AQSoft Practical Assignment - Denials Command Center - DATA PACK
=================================================================
All people, payers, facilities, NPIs and claims in this pack are FICTIONAL.
Treat "today" as 30 September 2026. Treat the data as if it were real patient health information.

Client: Gulfview Physician Partners (hospital and nursing-facility physicians, Florida)
Payers: Northstar Health Plan (NS401), Coastal Senior Advantage (CSA77),
        Sunshine Medicaid Partners (SMP12), Meridian PPO (MRD55)

FILES
-----
claims_export.csv                 Billing-system export, one row per claim LINE (Jan-Aug 2026 dates of service).
                                  Columns: claim_id, patient_first, patient_last, patient_dob, member_id, payer, payer_id,
                                  dos, submitted_date, rendering_npi, rendering_provider, facility, pos (21 = hospital,
                                  31 = nursing facility), line_no, cpt, modifier, units, charge, dx1-dx4 (ICD-10-CM),
                                  auth_number, coder_id, prebill_reviewed (Y/N).
remits/*.835                      Payer remittance files (ASC X12 835 5010), exactly as received from the clearinghouse.
denials_worklog.xlsx              The denials team's manual Excel tracker.
payer_policies/*.md               Payer policy excerpts (reimbursement, coding, authorization, enrollment).
payer_rules.csv                   Timely-filing, appeal and corrected-claim windows by payer (in days).
carc_rarc_reference.csv           Claim Adjustment Reason Codes (CARC) and Remittance Advice Remark Codes (RARC) used in the remits.
claim_adjustment_group_codes.csv  Meaning of the CAS group codes (CO, PR, OA, PI).
labeled_denials_sample.csv        40 denials reviewed by an expert: root_cause_category, owning_team, preventable_at_prebill.
                                  Use it to evaluate your AI. Do not hard-code these answers.

The assignment brief (PDF) explains what to build. Do not assume the data is clean, complete or consistent.
