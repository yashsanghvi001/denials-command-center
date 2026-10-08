import { ErrorAlert } from "../../components/Feedback";
import { StatsSkeleton } from "../../components/Skeletons";
import { bucketBadge, claimStatusBadge, daysLeftText, formatDate, formatMoney, splitWords } from "../../lib/format";
import { bucketLabel } from "../../lib/labels";
import { useClaim, useDenial } from "./queries";

export function ClaimHeader({ claimId }: { claimId: string }) {
  const claim = useClaim(claimId);
  const denial = useDenial(claimId);

  if (claim.isPending) {
    return (
      <section className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4">
          <div className="skeleton h-7 w-72" />
          <div className="skeleton h-4 w-96 max-w-full" />
          <StatsSkeleton count={3} />
        </div>
      </section>
    );
  }
  if (claim.isError) return <ErrorAlert error={claim.error} />;

  const { claim: header, state } = claim.data;
  const analysis = denial.data?.analysis;
  const deadlinePassed = analysis?.daysToDeadline != null && analysis.daysToDeadline < 0;

  return (
    <section className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {header.patientFirst} {header.patientLast}
            </h1>
            <p className="text-sm text-base-content/70">
              {header.claimId} · {header.payer} · Service on {formatDate(header.dateOfService)}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <span className={claimStatusBadge(state.status)}>{splitWords(state.status)}</span>
            {analysis && <span className={bucketBadge(analysis.bucket)}>{bucketLabel(analysis.bucket)}</span>}
            {analysis?.confidence === "Low" && <span className="badge badge-soft badge-warning">Needs review</span>}
          </div>
        </div>
        <div className="stats stats-vertical w-full bg-base-200/60 sm:stats-horizontal">
          <div className="stat">
            <div className="stat-title">Denied</div>
            <div className="stat-value text-2xl">{formatMoney(state.deniedAmount)}</div>
            <div className="stat-desc">of {formatMoney(state.billedCharge)} billed</div>
          </div>
          <div className="stat">
            <div className="stat-title">Expected back</div>
            {denial.isPending ? (
              <div className="skeleton mt-1 h-8 w-28" />
            ) : (
              <>
                <div className="stat-value text-2xl">{analysis ? formatMoney(analysis.expectedValue) : "None"}</div>
                <div className="stat-desc">{analysis ? "Based on this payer's history" : "No open denial"}</div>
              </>
            )}
          </div>
          <div className="stat">
            <div className="stat-title">Payer deadline</div>
            {denial.isPending ? (
              <div className="skeleton mt-1 h-8 w-28" />
            ) : (
              <>
                <div className="stat-value text-2xl">{analysis?.deadline ? formatDate(analysis.deadline) : "None"}</div>
                <div className={`stat-desc ${deadlinePassed ? "text-error" : ""}`}>{analysis ? daysLeftText(analysis.daysToDeadline) : ""}</div>
              </>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
