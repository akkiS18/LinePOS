# ==============================================================================
# Line POS - Production R8 Obfuscation & Security Proguard Rules
# ==============================================================================

# ------------------------------------------------------------------------------
# 1. Attributes & Crash Stack Tracing Preservation
# ------------------------------------------------------------------------------
-keepattributes *Annotation*,Signature,InnerClasses,EnclosingMethod
-renamesourcefileattribute SourceFile
-keepattributes SourceFile,LineNumberTable

# ------------------------------------------------------------------------------
# 2. Room Database & SQLite
# ------------------------------------------------------------------------------
-keep class * extends androidx.room.RoomDatabase
-dontwarn androidx.room.paging.**
-keep @androidx.room.Entity class * { *; }
-keep @androidx.room.Dao interface * { *; }
-keep class uz.pos.electro.data.local.entity.** { *; }
-keep class uz.pos.electro.data.local.dao.** { *; }
-keep class uz.pos.electro.data.local.converter.** { *; }
-keep class uz.pos.electro.data.local.relation.** { *; }
-dontwarn androidx.room.**

# ------------------------------------------------------------------------------
# 3. Data Models & Licensing
# ------------------------------------------------------------------------------
-keep class uz.pos.electro.data.model.** { *; }
-keepclassmembers class uz.pos.electro.data.model.** { *; }
-keep class uz.pos.electro.data.debt.** { *; }
-keepclassmembers class uz.pos.electro.data.debt.** { *; }
-keep class uz.pos.electro.data.licensing.** { *; }
-keepclassmembers class uz.pos.electro.data.licensing.** { *; }

# ------------------------------------------------------------------------------
# 4. Firebase Realtime Database
# ------------------------------------------------------------------------------
-keepclassmembers class * {
    @com.google.firebase.database.PropertyName <fields>;
}
-keep @com.google.firebase.database.IgnoreExtraProperties class * { *; }
-keep class com.google.firebase.database.** { *; }
-dontwarn com.google.firebase.**

# ------------------------------------------------------------------------------
# 5. Apache POI & XMLBeans (Excel .xlsx Export)
# ------------------------------------------------------------------------------
-dontwarn org.apache.poi.**
-keep class org.apache.poi.** { *; }
-dontwarn org.apache.xmlbeans.**
-keep class org.apache.xmlbeans.** { *; }
-dontwarn org.openxmlformats.**
-keep class org.openxmlformats.** { *; }
-dontwarn javax.xml.stream.**
-dontwarn java.awt.**
-dontwarn com.sun.source.**
-dontwarn org.w3c.dom.bootstrap.**
-dontwarn aQute.bnd.annotation.spi.**
-dontwarn org.osgi.framework.**
-dontwarn org.apache.logging.log4j.**

# ------------------------------------------------------------------------------
# 6. ML Kit & CameraX Barcode Scanning
# ------------------------------------------------------------------------------
-keep class com.google.mlkit.vision.barcode.** { *; }
-keep class com.google.android.gms.vision.** { *; }
-dontwarn com.google.mlkit.**
-dontwarn androidx.camera.**

# ------------------------------------------------------------------------------
# 7. Vico Charts & Jetpack Compose
# ------------------------------------------------------------------------------
-keep class com.patrykandpatrick.vico.** { *; }
-dontwarn com.patrykandpatrick.vico.**
-keep class androidx.compose.** { *; }
-dontwarn androidx.compose.**

# ------------------------------------------------------------------------------
# 8. Kotlin Coroutines & Dagger Hilt
# ------------------------------------------------------------------------------
-dontwarn kotlinx.coroutines.**
-keepclassmembers class kotlinx.coroutines.** { *; }
-dontwarn dagger.hilt.**
