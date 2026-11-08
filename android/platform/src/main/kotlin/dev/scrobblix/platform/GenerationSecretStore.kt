package dev.scrobblix.platform

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.AtomicFile
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.File
import java.security.KeyStore
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Unwired generation prototype. Legacy SessionSecretStore is unchanged byte-for-byte.
 * Caller supplies the one process-wide authority coordinator lock. No existing-generation cleanup or overwrite path.
 * A file or alias left after a failed write is an orphan, never silently repaired or adopted.
 */
class GenerationSecretStore(context:Context,private val coordinator:Any,private val stageBoundary:(String)->Unit={}) {
    private val directory=File(context.noBackupFilesDir,"scrobblix-credential-generations-v2")
    private fun canonical(id:String):String { require(UUID.fromString(id).toString()==id);return id }
    private fun target(id:String)=AtomicFile(File(directory,"${canonical(id)}.bin"))
    private fun alias(id:String)="scrobblix.credential.generation.v2.${canonical(id)}"
    private fun aad(id:String)=("scrobblix.credential.generation.v2\u0000"+canonical(id)).toByteArray(Charsets.UTF_8)
    private fun keys()=KeyStore.getInstance("AndroidKeyStore").apply{load(null)}
    fun stage(id:String,secret:ByteArray):Unit=synchronized(coordinator) {
        require(secret.isNotEmpty() && secret.size<=16384)
        val target=target(id);val keyAlias=alias(id)
        check(!target.baseFile.exists() && !File(target.baseFile.path+".bak").exists() && !File(target.baseFile.path+".new").exists() && !keys().containsAlias(keyAlias)) { "Generation identity already used" }
        check(directory.isDirectory || directory.mkdirs()) { "Generation directory unavailable" }
        val key=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore").run {
            init(KeyGenParameterSpec.Builder(keyAlias,KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256).setRandomizedEncryptionRequired(true).build());generateKey()
        }
        val cipher=Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE,key);cipher.updateAAD(aad(id))
        val encrypted=cipher.doFinal(secret)
        stageBoundary("beforeWrite")
        val stream=target.startWrite()
        try {
            DataOutputStream(stream).apply { writeInt(2);writeInt(cipher.iv.size);write(cipher.iv);writeInt(encrypted.size);write(encrypted);flush() }
            stageBoundary("afterWrite")
            target.finishWrite(stream)
        }catch(_:Exception){target.failWrite(stream);throw IllegalStateException("Generation write unavailable")}
        stageBoundary("beforeReadback")
        val verified=read(id) as? SessionSecretStore.ReadResult.Available ?: error("Generation readback unavailable")
        try { check(java.security.MessageDigest.isEqual(secret,verified.secret)) { "Generation readback mismatch" } }
        finally { verified.secret.fill(0) }
    }
    fun read(id:String):SessionSecretStore.ReadResult=synchronized(coordinator) {
        try {
            val target=target(id)
            if(!target.baseFile.exists() && !File(target.baseFile.path+".bak").exists())return@synchronized SessionSecretStore.ReadResult.Missing
            val key=keys().getKey(alias(id),null) as? SecretKey ?: return@synchronized SessionSecretStore.ReadResult.Unavailable
            DataInputStream(target.openRead()).use { input ->
                check(input.readInt()==2);check(input.readInt()==12)
                val iv=ByteArray(12);input.readFully(iv)
                val size=input.readInt();check(size in 17..16400)
                val bytes=ByteArray(size);input.readFully(bytes);check(input.read()==-1)
                val cipher=Cipher.getInstance("AES/GCM/NoPadding")
                cipher.init(Cipher.DECRYPT_MODE,key,GCMParameterSpec(128,iv));cipher.updateAAD(aad(id))
                SessionSecretStore.ReadResult.Available(cipher.doFinal(bytes))
            }
        }catch(_:Exception){SessionSecretStore.ReadResult.Unavailable}
    }
}
