package uz.pos.electro.data.local.relation

import androidx.room.Embedded
import androidx.room.Relation
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity

data class SaleWithItems(
    @Embedded
    val sale: SaleEntity,

    @Relation(
        parentColumn = "id",
        entityColumn = "sale_id"
    )
    val items: List<SaleItemEntity>
)
