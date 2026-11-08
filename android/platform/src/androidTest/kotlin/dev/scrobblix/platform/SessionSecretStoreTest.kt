package dev.scrobblix.platform

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File
import java.security.KeyStore
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class SessionSecretStoreTest {
    @Test fun actualKeystoreRoundTripAccountIsolationAndRemoval() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val account="synthetic-${UUID.randomUUID()}"
        val store=SessionSecretStore(context)
        try {
            assertEquals(SessionSecretStore.ReadResult.Missing,store.read("lastfm",account))
            store.write("lastfm",account,"synthetic-session".toByteArray())
            val read=SessionSecretStore(context).read("lastfm",account) as SessionSecretStore.ReadResult.Available
            assertArrayEquals("synthetic-session".toByteArray(),read.secret)
            assertEquals(SessionSecretStore.ReadResult.Missing,store.read("lastfm","other-$account"))
            store.write("lastfm",account,"replacement".toByteArray())
            assertArrayEquals("replacement".toByteArray(),(store.read("lastfm",account) as SessionSecretStore.ReadResult.Available).secret)
            store.remove("lastfm",account)
            assertEquals(SessionSecretStore.ReadResult.Missing,store.read("lastfm",account))
        } finally { store.remove("lastfm",account) }
    }
    @Test fun actualKeystoreCorruptionAndLostKeyFailClosed() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val account="synthetic-${UUID.randomUUID()}";val store=SessionSecretStore(context)
        val directory=File(context.noBackupFilesDir,"scrobblix-session-secrets")
        val before=directory.listFiles()?.map { it.name }?.toSet() ?: emptySet()
        val keys=KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        val beforeKeys=keys.aliases().toList().toSet()
        try {
            store.write("lastfm",account,"synthetic-only".toByteArray())
            val encrypted=directory.listFiles()!!.single { it.name !in before }
            val bytes=encrypted.readBytes();assertFalse(bytes.toString(Charsets.UTF_8).contains("synthetic-only"))
            bytes[bytes.lastIndex]=(bytes.last().toInt() xor 1).toByte();encrypted.writeBytes(bytes)
            assertEquals(SessionSecretStore.ReadResult.Unavailable,store.read("lastfm",account))
            store.write("lastfm",account,"synthetic-only".toByteArray())
            val alias=keys.aliases().toList().single { it !in beforeKeys }
            keys.deleteEntry(alias)
            assertEquals(SessionSecretStore.ReadResult.Unavailable,store.read("lastfm",account))
            assertFalse(keys.containsAlias(alias)) // Read must not silently replace a lost key.
        } finally { store.remove("lastfm",account) }
    }
}
