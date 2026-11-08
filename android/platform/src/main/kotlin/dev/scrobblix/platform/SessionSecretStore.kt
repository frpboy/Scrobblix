package dev.scrobblix.platform

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.AtomicFile
import java.io.ByteArrayOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.File
import java.io.FileNotFoundException
import java.security.KeyStore
import java.security.MessageDigest
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Local credential boundary only; not an authentication flow or credential backup service.
 * Single-process owner. No plaintext fallback, automatic destructive recovery, or secret logging.
 * Background queue use does not prompt biometrics. Hardware/StrongBox protection is not promised.
 */
class SessionSecretStore(context: Context) {
    private val directory=File(context.noBackupFilesDir,"scrobblix-session-secrets")
    sealed interface ReadResult {
        data object Missing:ReadResult
        data object Unavailable:ReadResult // Key loss, invalidation, corruption or I/O: reconnect required.
        class Available(val secret:ByteArray):ReadResult { override fun toString()="Available(redacted)" }
    }
    private fun identity(provider:String,account:String):String {
        require(provider.isNotBlank() && account.isNotBlank())
        val bytes=ByteArrayOutputStream().also { b -> DataOutputStream(b).use {
            it.writeUTF(provider);it.writeUTF(account)
        } }.toByteArray()
        return MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02x".format(it) }
    }
    private fun alias(id:String)="scrobblix.session.v1.$id"
    private fun file(id:String)=AtomicFile(File(directory,"$id.bin"))
    private fun keys()=KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
    private fun existingKey(id:String)=keys().getKey(alias(id),null) as? SecretKey
    private fun newKey(id:String):SecretKey=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore").run {
        init(KeyGenParameterSpec.Builder(alias(id),KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setKeySize(256).setRandomizedEncryptionRequired(true).build())
        generateKey()
    }
    @Synchronized fun write(provider:String,account:String,secret:ByteArray) {
        require(secret.isNotEmpty() && secret.size <= 16384)
        val id=identity(provider,account)
        val cipher=Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE,existingKey(id) ?: newKey(id))
        cipher.updateAAD(id.toByteArray(Charsets.UTF_8))
        val encrypted=cipher.doFinal(secret)
        check(directory.isDirectory || directory.mkdirs()) { "Credential directory unavailable" }
        val target=file(id);val stream=target.startWrite()
        try {
            val output=DataOutputStream(stream)
            output.writeInt(1);output.writeInt(cipher.iv.size);output.write(cipher.iv)
            output.writeInt(encrypted.size);output.write(encrypted);output.flush()
            target.finishWrite(stream)
        } catch(failure:Exception) { target.failWrite(stream);throw IllegalStateException("Credential write unavailable") }
    }
    @Synchronized fun read(provider:String,account:String):ReadResult {
        val id=identity(provider,account);val target=file(id)
        return try {
            val cipher=Cipher.getInstance("AES/GCM/NoPadding")
            DataInputStream(target.openRead()).use { input ->
                val key=existingKey(id) ?: return ReadResult.Unavailable // Never generate a replacement during read.
                check(input.readInt() == 1)
                val ivSize=input.readInt();check(ivSize == 12)
                val iv=ByteArray(ivSize);input.readFully(iv)
                val size=input.readInt();check(size in 17..16400)
                val encrypted=ByteArray(size);input.readFully(encrypted);check(input.read() == -1)
                cipher.init(Cipher.DECRYPT_MODE,key,GCMParameterSpec(128,iv))
                cipher.updateAAD(id.toByteArray(Charsets.UTF_8))
                ReadResult.Available(cipher.doFinal(encrypted))
            }
        } catch(missing:FileNotFoundException) {
            if(target.baseFile.exists()) ReadResult.Unavailable else ReadResult.Missing
        }
        catch(failure:Exception) { ReadResult.Unavailable }
    }
    /** Local sign-out only. Does not claim provider-side session revocation. */
    @Synchronized fun remove(provider:String,account:String) {
        val id=identity(provider,account)
        file(id).delete()
        keys().deleteEntry(alias(id))
    }
}
