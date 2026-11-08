package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager
class UnknownEvidenceTest {
 private val g="11111111-1111-4111-8111-111111111111"
 private fun scenario(block:(JdbcQueueDriver)->Unit) {
  val f=File.createTempFile("unknown-evidence-",".db");val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${f.absolutePath}"))
  try { LocalSchema.initialize(db,"a");block(db) }finally {db.connection.close();f.delete()}
 }
 private fun row(db:JdbcQueueDriver)=db.query("SELECT * FROM android_actions WHERE id='s'").single()
 private fun ready(db:JdbcQueueDriver):Pair<ConnectionAuthority,ConnectionAuthority.Lease> {
  DurableQueue(db).enqueue("s","lastfm","a",ActionType.Scrobble,0)
  val a=ConnectionAuthority(db,{ConnectionSchema.read(db)},Any());a.connect("a",g,0)
  val l=a.claim("s","w",{true},{false},1)!!;assertTrue(a.permit(l,{true},{false},2));return a to l
 }
 @Test fun v2MigrationPreservesEveryOldValueAndUnknownHasNoBackfill()=scenario { db ->
  DurableQueue(db).enqueue("s","lastfm","a",ActionType.Scrobble,0)
  db.execute("UPDATE android_actions SET state='SubmissionOutcomeUnknown',reason='provider.submission_outcome_unknown',provider_sends=2")
  db.execute("ALTER TABLE android_actions DROP COLUMN unknown_evidence");db.execute("PRAGMA user_version=2")
  val old=row(db);LocalSchema.initialize(db);LocalSchema.initialize(db)
  assertEquals(old,row(db).filterKeys{it!="unknown_evidence"});assertNull(row(db)["unknown_evidence"])
  assertEquals("3",db.query("PRAGMA user_version").single().values.single())
 }
 @Test fun v2InterruptedMigrationRollsBackColumnAndVersion()=scenario { db ->
  db.execute("ALTER TABLE android_actions DROP COLUMN unknown_evidence");db.execute("PRAGMA user_version=2")
  val fail=object:QueueSqlDriver by db {override fun execute(sql:String,args:List<Any?>):Int {
   if(sql=="PRAGMA user_version=3")error("synthetic");return db.execute(sql,args)
  }}
  assertThrows(IllegalStateException::class.java){LocalSchema.initialize(fail)}
  assertEquals("2",db.query("PRAGMA user_version").single().values.single())
  assertFalse(db.query("PRAGMA table_info(android_actions)").any{it["name"]=="unknown_evidence"})
  LocalSchema.initialize(db)
 }
 @Test fun everyEvidenceValueLeavesStateReasonSendBudgetAndDueIdentical() {
  var baseline:Map<String,String?>?=null
  for(e in listOf<UnknownEvidence?>(null)+UnknownEvidence.entries)scenario { db ->
   val(a,l)=ready(db);assertTrue(a.completeProvider(l,ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=e),10))
   val r=row(db);assertEquals(e?.name,r["unknown_evidence"])
   val unchanged=r.filterKeys{it!="unknown_evidence"}
   if(baseline==null)baseline=unchanged else assertEquals(baseline,unchanged)
   a.connect("a","22222222-2222-4222-8222-222222222222",11)
   assertNull(a.claim("s","new",{true},{false},12));assertEquals("SubmissionOutcomeUnknown",row(db)["state"])
  }
 }
 @Test fun staleCompletionCannotAttachEvidenceOrOverwriteRecoveredAttempt()=scenario { db ->
  val(a,l)=ready(db);a.recoverClaims(60002)
  val before=row(db);assertEquals("INTERRUPTED_SEND_RECOVERED",before["unknown_evidence"])
  assertFalse(a.completeProvider(l,ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=UnknownEvidence.PROVIDER_ERROR_8),60003))
  assertEquals(before,row(db))
 }
 @Test fun evidenceIsDiscardedForAcceptedRetryAuthPermanentAndNowPlaying() {
  for(result in listOf(ProviderResult(true,"provider.accepted"),ProviderResult(false,"provider.temporary_unavailable",true),ProviderResult(false,"provider.auth.invalid_session"),ProviderResult(false,"provider.rejected")))scenario {db ->
   val(a,l)=ready(db);a.completeProvider(l,result.copy(unknownEvidence=UnknownEvidence.PROVIDER_ERROR_8),10);assertNull(row(db)["unknown_evidence"])
  }
  scenario {db ->val(a,l)=ready(db);db.execute("UPDATE android_actions SET type='NowPlaying'")
   a.completeProvider(l,ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=UnknownEvidence.PROVIDER_ERROR_8),10);assertNull(row(db)["unknown_evidence"])}
 }
 @Test fun futureUnknownRawValuesAreNeverRenderedAsEnums() {
  assertNull(UnknownEvidence.read(null));assertNull(UnknownEvidence.read("<raw token body>"));assertNull(UnknownEvidence.read("FUTURE_STAGE"))
  assertEquals(UnknownEvidence.IO_AFTER_SEND_PERMIT,UnknownEvidence.read("IO_AFTER_SEND_PERMIT"))
 }

 @Test fun legacyUnfencedWorkerAndRecoveryNeverClaimPermitOrSendStartedEvidence()=scenario {db ->
  val h=ListeningHistory(db,DurableQueue(db));h.observe(LocalPlay("p","Artist","Title",null,0,170000,86000),"lastfm","a",0)
  val q=DurableQueue(db)
  assertTrue(QueueWorker(q,h,ScrobbleTransport{_,_,_->throw java.io.IOException()}).process("lastfm","a","w","synthetic",1))
  assertNull(db.query("SELECT unknown_evidence FROM android_actions").single()["unknown_evidence"])
  q.enqueue("s","lastfm","a",ActionType.Scrobble,0)
  assertNotNull(q.claim("lastfm","a","legacy",2,5));q.recover(8)
  assertNull(row(db)["unknown_evidence"])
 }
 @Test fun authorityRecoveryWithoutPermitIsHeldAndHasNoStageEvidence()=scenario {db ->
  DurableQueue(db).enqueue("s","lastfm","a",ActionType.Scrobble,0)
  val a=ConnectionAuthority(db,{ConnectionSchema.read(db)},Any());a.connect("a",g,0)
  assertNotNull(a.claim("s","w",{true},{false},1));a.recoverClaims(60002)
  assertEquals("LocalHold",row(db)["state"]);assertNull(row(db)["unknown_evidence"])
 }
 @Test fun independentHandleReadsFixedEvidenceWithoutEnablingResend() {
  val f=File.createTempFile("unknown-handle-",".db");val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${f.absolutePath}"))
  try {LocalSchema.initialize(db,"a");val(a,l)=ready(db)
   a.completeProvider(l,ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=UnknownEvidence.PROVIDER_ERROR_8),10)
   val other=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${f.absolutePath}"))
   try {assertEquals("PROVIDER_ERROR_8",row(other)["unknown_evidence"]);assertNull(ConnectionAuthority(other,{ConnectionSchema.read(other)},Any()).claim("s","again",{true},{false},11))}finally{other.connection.close()}
  }finally{db.connection.close();f.delete()}
 }

 @Test fun actualAuthorityWorkerRecordsOnlyFixedIoEvidenceAndKeepsUnknownHeld() {
  for(e in listOf<UnknownEvidence?>(null,UnknownEvidence.RESPONSE_JSON_INVALID,UnknownEvidence.RESPONSE_BODY_LIMIT,UnknownEvidence.RESPONSE_STREAM_ABSENT))scenario {db ->
   val h=ListeningHistory(db,DurableQueue(db));h.observe(LocalPlay("p","Artist","Title",null,0,170000,86000),"lastfm","a",0)
   val a=ConnectionAuthority(db,{ConnectionSchema.read(db)},Any());a.connect("a",g,0)
   var calls=0
   val w=AuthorityQueueWorker(a,db,h,ScrobbleTransport{_,_,_->calls++;throw(e?.let{ResponseEvidenceIOException(it)} ?: java.io.IOException("never-store-raw-error"))},{10})
   assertTrue(w.processNext(a.snapshot(),"w","synthetic",{true},{false}))
   val r=db.query("SELECT * FROM android_actions").single();assertEquals("SubmissionOutcomeUnknown",r["state"])
   assertEquals((e ?: UnknownEvidence.IO_AFTER_SEND_PERMIT).name,r["unknown_evidence"])
   assertFalse(r.values.any{it?.contains("never-store-raw-error")==true})
   assertFalse(w.processNext(a.snapshot(),"w","synthetic",{true},{false}));assertEquals(1,calls)
  }
 }
}
