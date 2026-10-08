import { StatsSkeleton } from "../../components/Skeletons";
import { formatMoney } from "../../lib/format";
import { useMoneySummary } from "./queries";

const cards = [
  { bucket: "Recoverable", title: "Can be recovered", tone: "text-success", note: "Appeal or correct and resubmit" },
  { bucket: "MaybeEligibility", title: "Check other coverage", tone: "text-warning", note: "Bill the patient's other insurance" },
  { bucket: "LostWindowExpired", title: "Deadline passed", tone: "", note: "Too late to appeal; close out" },
  { bucket: "LostPolicy", title: "Policy write-off", tone: "", note: "Payer policy does not pay; close out" },
];

export function MoneySummary() {
  const { data, isPending, isError } = useMoneySummary();
  if (isPending) return <StatsSkeleton count={4} />;
  if (isError) return null;

  const recoverable = data.buckets.find((bucket) => bucket.bucket === "Recoverable");
  return (
    <section aria-label="Money summary" className="space-y-3">
      <p className="text-sm text-base-content/70">
        <strong className="text-base-content">{formatMoney(data.deniedAmount)}</strong> denied across {data.openDenials} claims
        {recoverable && (
          <>
            ; about <strong className="text-base-content">{formatMoney(recoverable.expectedValue)}</strong> expected back
          </>
        )}
        .
      </p>
      <div className="stats stats-vertical w-full bg-base-100 shadow-sm lg:stats-horizontal">
        {cards.map((card) => {
          const bucket = data.buckets.find((candidate) => candidate.bucket === card.bucket);
          if (!bucket) return null;
          return (
            <div key={card.bucket} className="stat">
              <div className="stat-title">{card.title}</div>
              <div className={`stat-value text-2xl ${card.tone}`}>{formatMoney(bucket.deniedAmount)}</div>
              <div className="stat-desc">
                {bucket.claims} claims · {card.note}
              </div>
            </div>
          );
        })}
      </div>
      {data.dueWithin14Days.claims > 0 && (
        <div role="alert" className="alert alert-warning alert-soft">
          <span>
            <strong>{data.dueWithin14Days.claims} recoverable claims</strong> ({formatMoney(data.dueWithin14Days.deniedAmount)}) must be appealed or
            resubmitted within the next 14 days.
          </span>
        </div>
      )}
    </section>
  );
}
