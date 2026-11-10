import {DurableObject} from 'cloudflare:workers';
import {handler} from './broker.mjs';
import {ForwardBudget} from './counter.mjs';

export class ScrobblixBudget extends DurableObject {
  constructor(ctx,env){super(ctx,env);this.budget=new ForwardBudget(ctx,env);}
  consume(){return this.budget.consume();}
}
export default {
  async fetch(request,env) {
    return handler({gate:async(request,params)=>{
      if(!env.PER_IP || !env.FORWARD_BUDGET) return false;
      // Cloudflare-provided IP only. Ephemeral limiter key, never written to our count database.
      const ip=request.headers.get('CF-Connecting-IP');if(!ip)return false;
      const prefix=params.method.startsWith('auth.')?'auth:':'track:';
      const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(prefix+ip));
      const key=Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('');
      if(!(await env.PER_IP.limit({key})).success)return false;
      const id=env.FORWARD_BUDGET.idFromName('scrobblix-global-forward-budget-v1');
      return await env.FORWARD_BUDGET.get(id).consume();
    }})(request,env);
  }
};
