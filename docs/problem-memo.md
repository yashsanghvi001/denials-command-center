# Problem memo: Gulfview denials

Numbers are as of 30 September 2026, from the ERA files, the claims export and the worklog.

## The manager's three questions

1. **Money stuck:** 155 open denials worth **$31,145**.
   - **Still recoverable: $11,650** (55 claims). About $7,090 is expected back at each payer's historical pay rate.
   - **Depends on other coverage: $2,975** (14 claims).
   - **Already lost: $16,520.** $7,615 missed its appeal or correction deadline, and $8,905 is not payable under payer policy.
   - **Due soon:** 5 recoverable claims ($950) are due within 14 days.
2. **Why we are denied:** **131 of 155 denials ($25,880, 83%) could have been stopped before billing.**
   - **Coding errors: $10,915.** Of this, $5,295 is I10 billed with I11.x, $3,365 is a missing modifier 25 and $2,255 is frequency.
   - **Other causes:** provider not enrolled $4,460, missing authorization $4,210, eligibility $3,660, medical necessity $3,725, timely filing $1,725.
   - Only $1,540 (7 claims) is a payer error. Sunshine denied nursing-facility visits from before its 1 April authorization rule, and all 7 should be appealed.
3. **Who works on what today:** each specialist's queue is ordered by what can still be won, then by expected money × deadline urgency.

## Real problems found

**Business**
- **Pre-bill review is not catching the denials.** Claims that went through it still produced 78 of the 155 denials ($15,055). The review does not check the edits the payers actually enforce.
  - One coder (C07) accounts for $6,390 of preventable denials.
- **Most lost money is process, not payer.**
  - $4,460 lost because a provider billed Coastal before being enrolled. Enrollment is not retroactive.
  - All 19 missing-authorization denials ($4,210) are lost: 8 passed the 14-day retro-authorization window and 11 passed their appeal deadline.
  - In total, $7,615 of denials expired their deadline before anyone acted.
- **The worklog is wrong in ways that cost money:**
  - 22 items ($4,575) are marked closed while the payer still denies them.
  - 13 are still open although the claim was paid.
  - There are 17 ambiguous dates and 9 duplicate rows.
  - One note contains instructions aimed at an AI system.

**Data**
- **One ERA file is a resend:** era_2026Q2_resent_0719.835 repeats Q2. Summing the files naively overstates payments by **$47,461.62 (40%)**. Each payment gets a unique key, so it is counted once, and the remittances reconcile to **$0.00**.
- **3 payments ($260.40) belong to another practice's claim numbers.** They are listed as exceptions and never applied.
- **Payer reversals are matched to the payment they cancel.** 2 claims ($322.40) are overpaid and will be recouped.
- **The denial date** is the ERA payment date (BPR16), not the service date. Every appeal window counts from it.

## What I built first, and why

1. **Ingestion and reconciliation.** A number nobody trusts answers nothing, so ingestion comes first. It is idempotent, every exception has a reason, and the totals reconcile.
2. **Denial analysis.**
   - A rules engine built from the payer policies decides cause, team, preventability, next step and deadline. It is deterministic, explainable, and scores 40/40 on the expert labels.
   - Gemini only drafts letters, from de-identified facts, and gives a second opinion. Its output is validated against real policy sections, and a degraded mode works without it.
3. **The worklist.** It is what the team will use on Monday: roles, a priority queue, a claim timeline and a full audit trail.

## What I would build next

1. **Manager analytics and the prevention view,** with a JSON rules export for pre-bill. The data for both is already stored. The checks with the most impact:
   - I10 billed with I11.x
   - E/M billed with a procedure but no modifier 25
   - Sunshine initial nursing-facility visits on or after 1 April without an authorization number
   - provider enrolled with the payer on the date of service
   - submission within the timely-filing window
2. **Reopen the 22 items closed in error,** and file the 7 payer-error appeals this week.
3. **Coder feedback built from the prevention checks,** starting with C07.
