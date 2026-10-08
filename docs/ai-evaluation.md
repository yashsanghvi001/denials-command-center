# AI evaluation report

Both classifiers were scored against `data/labeled_denials_sample.csv`: 40 denials labelled by an expert with root cause, owning team and whether the denial was preventable before billing. To reproduce, sign in as the manager and call `GET /api/evaluation` (rules) and `POST /api/evaluation/ai` (Gemini). Gemini answers are cached, so a re-run costs nothing.

## Results

| Measure | Rules engine | Gemini 2.5 Flash (blind) |
|---|---|---|
| Root cause | 40/40 (100%) | 37/40 (92.5%) |
| Owning team | 40/40 (100%) | 32/40 (80%) |
| Preventable before billing | 40/40 (100%) | 32/40 (80%) |
| All three correct | 40/40 (100%) | 31/40 (77.5%) |
| Agrees with the rules on root cause | | 37/40 |

**"Blind"** means Gemini receives the de-identified claim and remittance facts and the payer policy excerpts, but **not** the rules engine's answer, so its score is independent.

## Where Gemini fails, and why

All 9 failures sit in two places:

1. **Medical necessity (CARC 50): 0 of 8 fully correct.**
   - **Wrong owning team, 7 times.** It answered "Denials (appeal)", reasoning that the next step is an appeal. The expert convention is that the clinical documentation team owns these ("Coding / Clinical"), because the fix is a better record, not a better letter. That convention appears nowhere in the data the model sees.
   - **Wrong preventability, 8 times.** It answered "Unknown" or "Yes". The expert says "No": a medical-necessity judgement cannot be caught by a pre-bill edit.
   - **Wrong root cause, 3 times.** It called it "Coding - frequency" or "Coding - diagnosis". Those claims also carry same-day visits or hypertension codes, and the model weighted those signals over the denial code itself.
2. **Frequency (CARC 151), owning team, 1 claim.** Same reasoning error: the next step is an appeal, so the model gave the claim to the appeal team instead of Coding.

**The pattern:** Gemini reads the clinical and coding facts well, but it does not know the team's house conventions for who owns a denial and what counts as preventable at pre-bill. These are business definitions, not facts in the claim.

## How the product handles this

- **Rules decide, Gemini assists.** The rules engine sets cause, team, preventability, next action, deadline and money. Gemini's classification is a second opinion, and its letter is used only if it passes the checks below.
- **Disagreement goes to a person.** When Gemini drafts a letter, its classification is compared with the rules:
  - A different root cause rejects the letter, and the rule-based template is used instead.
  - A different team or preventability sends the claim to the review queue.
- **Grounding is checked.** A draft that cites a policy section that does not exist is rejected.
- **Degraded mode.** Without the AI (no key, outage, quota) every number and every letter still comes from the rules and templates.

## Honest limits of this evaluation

- **40 labels is a small sample.** One error moves accuracy by 2.5 points.
- **The rules' 40/40 is optimistic.** The rules were written from the payer policies and the CARC/RARC descriptions, not from the labels. But I could see the labelled file while building, and the rules engine has a rule for every reason code in this sample. A held-out set from new months is the real test.
- **Gemini's score depends on the prompt.** Adding the team's ownership and preventability definitions to the prompt, or a few labelled examples, would very likely fix the medical-necessity failures. I deliberately did not tune the prompt on these 40 labels, so the score stays an honest blind measure.
- **The cached answers mean a re-run reproduces this exact result.** A new model version, or any prompt change, starts a fresh cache and needs a fresh evaluation.
