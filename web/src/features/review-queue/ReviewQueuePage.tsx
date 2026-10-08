import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { EmptyState, ErrorAlert } from "../../components/Feedback";
import { PageHeader } from "../../components/PageHeader";
import { TableSkeletonRows } from "../../components/Skeletons";
import { api } from "../../lib/api";
import { appConfig } from "../../lib/config";
import { bucketBadge, formatMoney } from "../../lib/format";
import { bucketLabel } from "../../lib/labels";
import type { ReviewItem } from "../../lib/types";

export function ReviewQueuePage() {
  const { data, isPending, isError, error } = useQuery({
    queryKey: ["review"],
    queryFn: () => api.get<ReviewItem[]>("/api/review-queue"),
    staleTime: appConfig.freshness.lists,
    refetchOnWindowFocus: true,
  });

  return (
    <div className="space-y-5">
      <PageHeader title="Review queue" subtitle="Claims a person should double-check: the rules were unsure, the AI disagreed with the rules, or an AI letter failed our checks." />
      {isError && <ErrorAlert error={error} />}
      <section className="card overflow-hidden bg-base-100 shadow-sm">
        <div className="overflow-x-auto">
          <table className="table table-sm table-zebra">
            <thead>
              <tr>
                <th>Claim</th>
                <th>Why denied</th>
                <th>Confidence</th>
                <th>Why it needs review</th>
                <th>Outlook</th>
                <th className="text-right">Denied</th>
              </tr>
            </thead>
            <tbody>
              {isPending && <TableSkeletonRows rows={5} columns={6} />}
              {data?.length === 0 && (
                <tr>
                  <td colSpan={6}>
                    <EmptyState title="Nothing needs review" />
                  </td>
                </tr>
              )}
              {data?.map(({ analysis, reason }) => (
                <tr key={analysis.claimId}>
                  <td className="whitespace-nowrap">
                    <Link to={`/claims/${encodeURIComponent(analysis.claimId)}?tab=denial`} className="link link-hover link-primary font-medium">{analysis.claimId}</Link>
                  </td>
                  <td>{analysis.rootCause}</td>
                  <td>{analysis.confidence}</td>
                  <td>{reason}</td>
                  <td><span className={bucketBadge(analysis.bucket)}>{bucketLabel(analysis.bucket)}</span></td>
                  <td className="text-right tabular-nums">{formatMoney(analysis.deniedAmount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
