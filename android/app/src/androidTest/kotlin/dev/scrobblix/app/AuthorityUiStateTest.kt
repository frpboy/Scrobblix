package dev.scrobblix.app

import android.content.Context
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.CompletableFuture

/** Synthetic isolated-store pixels only. No browser launch, provider or account secrets. */
class AuthorityUiStateTest {
    @get:Rule val ui=createComposeRule()
    private fun context():Context {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val prefix="authority-pixels-"+java.util.UUID.randomUUID()
        val directory=java.io.File(base.noBackupFilesDir,prefix).apply{mkdirs()}
        return object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=java.io.File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("$prefix-$name",mode)
        }
    }
    private fun capture(name:String) {
        ui.waitForIdle()
        // No state mutation or secure-window override.
        InstrumentationRegistry.getInstrumentation().uiAutomation.executeShellCommand("screencap -p /sdcard/Download/scrobblix-$name.png").use {
            android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes()
        }
    }
    private fun settings()=ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
    private fun show(store:AppStore) {
        ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser launch allowed")})}}
    }
    @Test fun unavailableAuthorityDisablesLoginAndShowsExplicitState() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.sql.execute("DELETE FROM android_connection")
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Local connection storage unavailable. Delivery is paused.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Local connection storage unavailable. Delivery is paused.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Authorize in browser").assertDoesNotExist()
            ui.onNodeWithText("Disconnect Last.fm").assertDoesNotExist()
            capture("authority-unavailable")
        }finally{store.close()}
    }
    @Test fun durableConsentGateIsVisibleForSavedSyntheticAccount() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))},{false})
        try {
            assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticPixelAccount","a".repeat(32)).state)
            assertFalse(store.setTracking(false).committed)
            assertTrue(store.authority.snapshot().resumeConsentRequired)
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Choose tracking before collection or delivery resumes.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Choose tracking before collection or delivery resumes.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Connected as SyntheticPixelAccount").assertExists()
            capture("authority-consent")
        }finally{store.close()}
    }
    @Test fun asynchronousLocalSetupErrorDoesNotInviteNewAuthorization() {
        val failed=CompletableFuture<Unit>().apply{completeExceptionally(java.io.IOException("synthetic setup failure"))}
        val store=AppStore(context(),{listOf(failed)})
        try {
            store.setTracking(true)
            assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticPixelAccount","b".repeat(32)).state)
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Local work setup is deferred. The saved account does not need another authorization.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Local work setup is deferred. The saved account does not need another authorization.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Connected as SyntheticPixelAccount").assertExists()
            ui.onNodeWithText("Authorize in browser").assertDoesNotExist()
            capture("authority-setup-error")
        }finally{store.close()}
    }
}
