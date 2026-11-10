/** One singleton object, SQLite backend. Counts/day only; never session, IP or listening data. */
export class ForwardBudget {
  constructor(ctx,env) {
    this.ctx=ctx;
    this.limit=Number(env.DAILY_FORWARD_LIMIT??5000);
    if(!Number.isSafeInteger(this.limit) || this.limit<1 || this.limit>5000) throw Error('invalid budget');
    ctx.storage.sql.exec('CREATE TABLE IF NOT EXISTS budget (id INTEGER PRIMARY KEY CHECK(id=1), day TEXT NOT NULL, total INTEGER NOT NULL CHECK(total>=0))');
  }
  consume() {
    const day=new Date().toISOString().slice(0,10);
    return this.ctx.storage.transactionSync(()=>{
      this.ctx.storage.sql.exec('INSERT INTO budget(id,day,total) VALUES(1,?,0) ON CONFLICT(id) DO UPDATE SET day=excluded.day,total=0 WHERE budget.day<>excluded.day',day);
      const row=this.ctx.storage.sql.exec('SELECT total FROM budget WHERE id=1').toArray()[0];
      if(row.total>=this.limit) return false;
      this.ctx.storage.sql.exec('UPDATE budget SET total=total+1 WHERE id=1');
      return true;
    });
  }
}
