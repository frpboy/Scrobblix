# Content Design

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Voice

Direct, factual, calm, and specific. Avoid blame and vague reassurance.

## Preferred patterns

- “Scrobblix received a title but no artist from Chrome.”
- “Played 1:42 of the required 2:15.”
- “Saved locally. Last.fm will be retried at 21:40.”
- “This appears to match a scrobble already accepted at 21:31.”

## Avoid

- “Oops!”
- “Something went wrong.”
- “Invalid parameters” without field context.
- “Success” before provider acknowledgement.
- “Unsupported” without explanation or next action.

## Reason message template

**What happened** — one sentence.  
**Why** — evidence and policy.  
**Impact** — whether local history/provider submission is affected.  
**Next step** — one primary action and optional secondary detail.

## Terminology consistency

- Use “scrobble” as a noun/verb where target users understand it; onboarding defines it.
- Use “Browser Companion,” not “plugin.”
- Use “Windows media controls” in general UI; “GSMTC” only in technical details.
- Use “saved locally” versus “submitted to Last.fm.”
- Use “ignored” only when policy/provider intentionally rejected; use “not eligible” for insufficient play.
