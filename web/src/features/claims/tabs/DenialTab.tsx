import { Detail, DetailList } from "../../../components/DetailList";
import { EmptyState, ErrorAlert } from "../../../components/Feedback";
import { SectionCard } from "../../../components/SectionCard";
import { DetailSkeleton } from "../../../components/Skeletons";
import { citationLabel, draftCaption, preventableText } from "../claimText";
import { useDenial, useDraftLetter, useReference } from "../queries";

export function DenialTab({ claimId }: { claimId: string }) {
  const denial = useDenial(claimId);
  const reference = useReference();
  const draftLetter = useDraftLetter(claimId);

  if (denial.isPending) return <SectionCard title="Why it was denied"><DetailSkeleton lines={9} /></SectionCard>;
  if (denial.isError) return <ErrorAlert error={denial.error} />;
  if (!denial.data) {
    return (
      <SectionCard title="Why it was denied">
        <EmptyState title="No open denial" hint="This claim is paid or was never denied, so there is nothing to appeal." />
      </SectionCard>
    );
  }

  const { analysis, draft } = denial.data;
  const reasonCodes = analysis.reasonCodes.split(/[,;\s]+/).filter(Boolean);

  return (
    <div className="grid gap-5 xl:grid-cols-5">
      <div className="xl:col-span-3">
        <SectionCard title="Why it was denied and what to do">
          <DetailList>
            <Detail label="Payer's reason">
              {reasonCodes.map((code) => (
                <div key={code}>
                  {reference.data ? (
                    (reference.data.reasonCodes[code] ?? "Reason code not in our reference list.")
                  ) : reference.isPending ? (
                    <span className="skeleton inline-block h-4 w-56 align-middle" />
                  ) : (
                    `Reason code ${code}`
                  )}{" "}
                  <span className="text-xs text-base-content/60">(code {code})</span>
                </div>
              ))}
            </Detail>
            <Detail label="Cause">{analysis.rootCause}</Detail>
            <Detail label="Team to fix it">{analysis.owningTeam}</Detail>
            <Detail label="Preventable?">{preventableText(analysis.preventable)}</Detail>
            <Detail label="Confidence">
              {analysis.confidence === "Low" ? <span className="badge badge-soft badge-warning">Low, needs review</span> : analysis.confidence}
              {analysis.confidenceReason && <p className="mt-1 text-base-content/60">{analysis.confidenceReason}</p>}
            </Detail>
            <Detail label="What to do next">
              <span className="font-medium">{analysis.nextAction}</span>
            </Detail>
            <Detail label="Payer policy">
              {analysis.citations.length === 0 && "No specific payer policy applies."}
              {analysis.citations.map((citation) => {
                const { title, text } = citationLabel(citation, reference.data);
                return (
                  <div key={citation} className="mb-2">
                    <div>{title}</div>
                    {text && <blockquote className="border-l-2 border-base-300 pl-3 text-base-content/70">{text}</blockquote>}
                  </div>
                );
              })}
            </Detail>
          </DetailList>
        </SectionCard>
      </div>
      <div className="xl:col-span-2">
        <SectionCard
          title="Appeal letter"
          actions={
            <button type="button" className="btn btn-primary btn-sm" disabled={draftLetter.isPending} onClick={() => draftLetter.mutate()}>
              {draftLetter.isPending && <span className="loading loading-spinner loading-xs" />}
              {draft ? "Draft again" : "Draft appeal letter"}
            </button>
          }
        >
          {draft ? (
            <div className="space-y-3">
              <p className="text-sm text-base-content/70">{draftCaption(draft)}</p>
              {draft.letter && <pre className="whitespace-pre-wrap rounded-box bg-base-200 p-4 font-sans text-sm leading-relaxed">{draft.letter}</pre>}
            </div>
          ) : (
            <p className="text-sm text-base-content/70">
              No letter yet. Drafting uses the AI when it is switched on; asking again for the same facts is answered from the cache at no cost.
            </p>
          )}
        </SectionCard>
      </div>
    </div>
  );
}
