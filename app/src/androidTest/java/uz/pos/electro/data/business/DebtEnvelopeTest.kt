package uz.pos.electro.data.business

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Test

class DebtEnvelopeTest {
    @Test fun sharedFrozenEnvelopeContractOnAndroidRuntime() {
        val input=InstrumentationRegistry.getInstrumentation().context.assets.open("debt-envelope-fixtures.json").bufferedReader().use { it.readText() }
        EnvelopeRunner.run(input)
    }
}
