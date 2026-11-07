package dev.scrobblix.platform

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import java.util.UUID

/** Synthetic only. No provider, owner keys or network. Unrun until explicitly qualified. */
class GenerationSecretPrototypeTest {
    @Test fun separateNamespaceReadbackRefuseOverwriteAndLegacyPreservation() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val lock=Any();val gen=GenerationSecretStore(context,lock);val legacy=SessionSecretStore(context)
        val id=UUID.randomUUID().toString()
        val old="synthetic-legacy".toByteArray();val fresh="synthetic-generation".toByteArray()
        // Same text does not imply same credential identity or key/AAD namespace.
        legacy.write("lastfm",id,old)
        try {
            gen.stage(id,fresh)
            val a=gen.read(id) as SessionSecretStore.ReadResult.Available
            try {assertArrayEquals(fresh,a.secret)}finally{a.secret.fill(0)}
            assertThrows(IllegalStateException::class.java){gen.stage(id,"overwrite".toByteArray())}
            val b=legacy.read("lastfm",id) as SessionSecretStore.ReadResult.Available
            try{assertArrayEquals(old,b.secret)}finally{b.secret.fill(0)}
            val c=gen.read(id) as SessionSecretStore.ReadResult.Available
            try{assertArrayEquals(fresh,c.secret)}finally{c.secret.fill(0)}
        }finally{old.fill(0);fresh.fill(0);legacy.remove("lastfm",id)}
        // The test intentionally does not introduce a production generation-deletion API.
    }
    @Test fun fileAliasAndAtomicResidueCollisionsRefuseStaging() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val gen=GenerationSecretStore(context,Any())
        val dir=java.io.File(context.noBackupFilesDir,"scrobblix-credential-generations-v2").apply{mkdirs()}
        for(suffix in listOf(".bin",".bin.new",".bin.bak")) {
            val id=UUID.randomUUID().toString();val file=java.io.File(dir,id+suffix)
            file.writeText("synthetic-existing")
            assertThrows(IllegalStateException::class.java){gen.stage(id,"new".toByteArray())}
            assertEquals("synthetic-existing",file.readText());file.delete()
        }
        val id=UUID.randomUUID().toString();val alias="scrobblix.credential.generation.v2.$id"
        val generator=javax.crypto.KeyGenerator.getInstance(android.security.keystore.KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore")
        generator.init(android.security.keystore.KeyGenParameterSpec.Builder(alias,android.security.keystore.KeyProperties.PURPOSE_ENCRYPT or android.security.keystore.KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(android.security.keystore.KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(android.security.keystore.KeyProperties.ENCRYPTION_PADDING_NONE).build())
        generator.generateKey()
        try {
            val keys=java.security.KeyStore.getInstance("AndroidKeyStore").apply{load(null)}
            val before=keys.getKey(alias,null)
            assertThrows(IllegalStateException::class.java){gen.stage(id,"new".toByteArray())}
            assertTrue(keys.containsAlias(alias));assertEquals(before,keys.getKey(alias,null))
            assertFalse(java.io.File(dir,"$id.bin").exists());assertFalse(java.io.File(dir,"$id.bin.new").exists())
        }
        finally {java.security.KeyStore.getInstance("AndroidKeyStore").apply{load(null);deleteEntry(alias)}}
        assertTrue(gen.read("not-a-uuid") is SessionSecretStore.ReadResult.Unavailable)
    }
    @Test fun missingKeyAfterStageReturnsUnavailableWithoutChangingOldLegacySecret() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val gen=GenerationSecretStore(context,Any());val old=SessionSecretStore(context)
        val id=UUID.randomUUID().toString();old.write("lastfm",id,"old".toByteArray())
        try {
            gen.stage(id,"new".toByteArray())
            java.security.KeyStore.getInstance("AndroidKeyStore").apply{load(null);deleteEntry("scrobblix.credential.generation.v2.$id")}
            assertTrue(gen.read(id) is SessionSecretStore.ReadResult.Unavailable)
            val legacy=old.read("lastfm",id) as SessionSecretStore.ReadResult.Available
            try{assertArrayEquals("old".toByteArray(),legacy.secret)}finally{legacy.secret.fill(0)}
        } finally {old.remove("lastfm",id)}
    }

    @Test fun injectedStageWriteAndReadbackFailurePreserveExistingLegacyCredential() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val legacy=SessionSecretStore(context)
        for(boundary in listOf("beforeWrite","afterWrite","beforeReadback")) {
            val id=UUID.randomUUID().toString();legacy.write("lastfm",id,"old".toByteArray())
            val gen=GenerationSecretStore(context,Any()) { point -> if(point==boundary)error("synthetic stage fault") }
            try {
                assertThrows(IllegalStateException::class.java){gen.stage(id,"new".toByteArray())}
                val old=legacy.read("lastfm",id) as SessionSecretStore.ReadResult.Available
                try{assertArrayEquals("old".toByteArray(),old.secret)}finally{old.secret.fill(0)}
                // Alias/orphan remains unusable for a second stage, never silently repaired.
                assertThrows(IllegalStateException::class.java){GenerationSecretStore(context,Any()).stage(id,"retry".toByteArray())}
            }finally{legacy.remove("lastfm",id)}
        }
    }

    @Test fun actualReadbackCorruptionAndKeyLossRefusePublicationAndPreserveLegacy() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val legacy=SessionSecretStore(context)
        for(loseKey in listOf(false,true)) {
            val id=UUID.randomUUID().toString();legacy.write("lastfm",id,"old".toByteArray())
            val gen=GenerationSecretStore(context,Any()) { point -> if(point=="beforeReadback") {
                if(loseKey)java.security.KeyStore.getInstance("AndroidKeyStore").apply{load(null);deleteEntry("scrobblix.credential.generation.v2.$id")}
                else java.io.File(context.noBackupFilesDir,"scrobblix-credential-generations-v2/$id.bin").writeText("synthetic invalid ciphertext")
            } }
            try {
                assertThrows(IllegalStateException::class.java){gen.stage(id,"new".toByteArray())}
                val old=legacy.read("lastfm",id) as SessionSecretStore.ReadResult.Available
                try{assertArrayEquals("old".toByteArray(),old.secret)}finally{old.secret.fill(0)}
            }finally{legacy.remove("lastfm",id)}
        }
    }

}
