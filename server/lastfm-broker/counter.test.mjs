import test from 'node:test';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {ForwardBudget} from './counter.mjs';
const context=db=>({storage:{sql:{exec:(sql,...args)=>({toArray:()=>db.prepare(sql).all(...args)})},transactionSync:fn=>{
 db.exec('BEGIN');try{const result=fn();db.exec('COMMIT');return result;}catch(e){db.exec('ROLLBACK');throw e;}
}}});
// Runtime SQL exec executes eagerly, including statements whose cursor isn't consumed.
const runtimeContext=db=>{const ctx=context(db);ctx.storage.sql.exec=(sql,...args)=>{const statement=db.prepare(sql);const rows=statement.all(...args);return {toArray:()=>rows};};return ctx;};
test('exact bounded global forwarding count persists across object reopen',()=>{
 const db=new DatabaseSync(':memory:');try {
  const ctx=runtimeContext(db);const budget=new ForwardBudget(ctx,{DAILY_FORWARD_LIMIT:3});
  assert.equal(budget.consume(),true);assert.equal(budget.consume(),true);
  const reopened=new ForwardBudget(ctx,{DAILY_FORWARD_LIMIT:3});
  assert.equal(reopened.consume(),true);assert.equal(reopened.consume(),false);
  assert.equal(db.prepare('SELECT total FROM budget').get().total,3);
 }finally{db.close();}
});
test('UTC day resets one row and stores only day/count',()=>{
 const db=new DatabaseSync(':memory:');try {
  const budget=new ForwardBudget(runtimeContext(db),{DAILY_FORWARD_LIMIT:1});budget.consume();
  db.prepare('UPDATE budget SET day=?').run('2000-01-01');assert.equal(budget.consume(),true);
  assert.equal(db.prepare('SELECT COUNT(*) AS n FROM budget').get().n,1);
  assert.deepEqual(db.prepare('PRAGMA table_info(budget)').all().map(x=>x.name),['id','day','total']);
 }finally{db.close();}
});
test('invalid and excessive forwarding budget fails closed',()=>{
 const db=new DatabaseSync(':memory:');try{for(const limit of [0,-1,5001,'bad'])assert.throws(()=>new ForwardBudget(runtimeContext(db),{DAILY_FORWARD_LIMIT:limit}));}finally{db.close();}
});
