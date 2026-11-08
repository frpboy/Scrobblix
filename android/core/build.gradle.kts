plugins { id("org.jetbrains.kotlin.jvm") }
kotlin { jvmToolchain(17) }
dependencies { testImplementation("junit:junit:4.13.2") }
dependencies { testImplementation("org.xerial:sqlite-jdbc:3.54.1.0") }
