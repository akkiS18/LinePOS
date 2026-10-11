using System;
using System.Linq;

namespace PosElectro.Desktop.Debt;

public sealed record DebtCustomerEditCommand(string RequestGuid, string StoreGuid, string CustomerGuid,
    string BeforeName, string BeforePhone, string BeforeNote, bool BeforeArchived,
    string Name, string Phone, string Note, bool Archived);
public sealed record DebtCustomerEditResult(string Status, DebtCustomerRecord? Customer);

public sealed partial class DebtRepository
{
    public const string CustomerEditCapability = "debt_customer_edit_v1";

    // Field-level compare-and-set + receipt commit atomically. A response lost on LAN
    // must not apply the edit twice or overwrite a later desktop edit on replay.
    public DebtCustomerEditResult ApplyCustomerEdit(DebtCustomerEditCommand q)
    {
        Id(q.RequestGuid); Id(q.CustomerGuid); Id(q.StoreGuid); Need(q.StoreGuid == store);
        TextField(q.Name,256,true); TextField(q.Phone,64); TextField(q.Note,2048);
        TextField(q.BeforeName,256,true); TextField(q.BeforePhone,64); TextField(q.BeforeNote,2048);
        var hash = Hash(Canonical("customer_edit_v1",store,q.RequestGuid,q.CustomerGuid,
            q.BeforeName,q.BeforePhone,q.BeforeNote,q.BeforeArchived?"1":"0",
            q.Name,q.Phone,q.Note,q.Archived?"1":"0"));
        return Write((c,t) => {
            Scope(c,t);
            var row = Rows(c,t,"SELECT name,phone,note,archived,revision,created_at FROM debt_customers WHERE guid=@p0 AND store_guid=@p1",q.CustomerGuid,store).SingleOrDefault();
            if(row == null) return new DebtCustomerEditResult("MissingCustomer",null);
            var current = new DebtCustomerRecord(q.CustomerGuid,store,(string)row[0]!, (string)row[1]!, (string)row[2]!,
                Convert.ToInt64(row[3])!=0,Convert.ToInt64(row[4]),Convert.ToInt64(row[5]));
            var receiptKey = "debt_customer_edit_receipt:"+q.RequestGuid;
            var receipt = (string?)Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",receiptKey);
            if(receipt != null) {
                Need(receipt == hash);
                return new DebtCustomerEditResult("AlreadyApplied",current);
            }
            static bool Conflict<T>(T before,T desired,T actual) =>
                !Equals(before,desired) && !Equals(actual,before) && !Equals(actual,desired);
            if(Conflict(q.BeforeName,q.Name,current.Name) || Conflict(q.BeforePhone,q.Phone,current.Phone) ||
                Conflict(q.BeforeNote,q.Note,current.Note) || Conflict(q.BeforeArchived,q.Archived,current.Archived))
                return new DebtCustomerEditResult("Conflict",current);

            var name = q.BeforeName == q.Name ? current.Name : q.Name;
            var phone = q.BeforePhone == q.Phone ? current.Phone : q.Phone;
            var note = q.BeforeNote == q.Note ? current.Note : q.Note;
            var archived = q.BeforeArchived == q.Archived ? current.Archived : q.Archived;
            var changed = name!=current.Name || phone!=current.Phone || note!=current.Note || archived!=current.Archived;
            if(changed) {
                Exec(c,t,"UPDATE debt_customers SET name=@p0,phone=@p1,note=@p2,archived=@p3,revision=revision+1 WHERE guid=@p4 AND store_guid=@p5",
                    name,phone,note,archived?1:0,q.CustomerGuid,store);
                current = current with {Name=name,Phone=phone,Note=note,Archived=archived,Revision=checked(current.Revision+1)};
            }
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",receiptKey,hash);
            return new DebtCustomerEditResult("Applied",current);
        });
    }
}
