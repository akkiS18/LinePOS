import java.io.File
import org.json.JSONArray
import org.json.JSONObject
import uz.pos.electro.data.debt.*

private fun account(n: JSONObject) = DebtAccount(n.getString("id"), n.getString("sale"), n.getString("customer"), n.getLong("at"), n.getLong("balance"))
private fun accounts(n: JSONObject): List<DebtAccount> = n.getJSONArray("accounts").let { a -> (0 until a.length()).map { account(a.getJSONObject(it)) } }
private fun effect(e: DebtEffect) = JSONObject().put("lines", JSONArray(e.lines.map { listOf(it.accountGuid, it.deltaMinor) }))
    .put("cash",e.cashMinor).put("card",e.cardMinor).put("fee",e.feeExpenseMinor).put("revenue",e.revenueMinor)
private fun payment(n: JSONObject): DebtEffect {
    val a = n.getJSONArray("allocations")
    return DebtAccounting.payment(n.getLong("cash"), n.getLong("card"), n.getLong("fee"),
        (0 until a.length()).map { DebtAllocation(a.getJSONArray(it).getString(0), a.getJSONArray(it).getLong(1)) })
}
private fun execute(n: JSONObject): Any = when (n.getString("op")) {
    "parse" -> DebtAccounting.parseUzs(n.getString("text"))
    "sale" -> DebtAccounting.newDebt(n.getLong("total"), n.getLong("cash"), n.getLong("card"))
    "balance" -> DebtAccounting.balance(n.getLong("original"), n.getJSONArray("deltas").let { a -> (0 until a.length()).map { a.getLong(it) } })
    "totals" -> DebtAccounting.totals(accounts(n)).let { JSONArray(listOf(it.receivableMinor,it.creditMinor)) }
    "allocate" -> JSONArray(DebtAccounting.allocate(n.getLong("amount"), n.getString("customer"), accounts(n),
        if (n.has("target")) n.getString("target") else null).map { listOf(it.accountGuid,it.amountMinor) })
    "payment" -> effect(payment(n))
    "reverse" -> effect(DebtAccounting.reversePayment(payment(n),n.getLong("refundedFee")))
    "return" -> DebtAccounting.splitReturn(n.getLong("value"),n.getLong("balance")).let { JSONArray(listOf(it.debtOffsetMinor,it.refundMinor)) }
    "transfer" -> effect(DebtAccounting.transferCredit(account(n.getJSONObject("source")),account(n.getJSONObject("target")),n.getLong("amount")))
    "refund" -> effect(DebtAccounting.refundCredit(account(n.getJSONObject("account")),n.getLong("cash"),n.getLong("card")))
    else -> error("Unknown fixture operation")
}
fun main(args: Array<String>) {
    require(args.size == 2) { "Usage: fixtures.json result.json" }
    val fixtures = JSONArray(File(args[0]).readText()); val output = JSONObject()
    for (i in 0 until fixtures.length()) {
        val f = fixtures.getJSONObject(i)
        val actual = try { execute(f) }
        catch (e: ArithmeticException) { JSONObject().put("error","overflow") }
        catch (e: IllegalArgumentException) { JSONObject().put("error","invalid") }
        // JSONObject.similar compares numeric values rather than Integer/Long wrapper types.
        check(JSONObject().put("value",actual).similar(JSONObject().put("value",f.get("expected")))) {
            "${f.getString("id")}: expected ${f.get("expected")}, actual $actual"
        }
        output.put(f.getString("id"),actual)
    }
    File(args[1]).writeText(output.toString())
    println("Kotlin: ${fixtures.length()} shared debt fixtures passed")
}
