import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { EmptyState, ErrorAlert } from "../../components/Feedback";
import { Pagination } from "../../components/grid/Pagination";
import { SearchInput } from "../../components/grid/SearchInput";
import { SortableHeader } from "../../components/grid/SortableHeader";
import { PageHeader } from "../../components/PageHeader";
import { TableSkeletonRows } from "../../components/Skeletons";
import { api } from "../../lib/api";
import { appConfig } from "../../lib/config";
import { formatMoney, severityBadge } from "../../lib/format";
import { issueHint, issueLabel, issueLocation } from "../../lib/labels";
import type { ExceptionsPage as ExceptionsResult } from "../../lib/types";
import { useGridParams } from "../../lib/useGridParams";

const severities = [
  { value: "Error", label: "Errors (money at risk)" },
  { value: "Warning", label: "Warnings (needs a look)" },
  { value: "Info", label: "Info (no action needed)" },
];

export function ExceptionsPage() {
  const { params, update } = useGridParams();
  const query = params.toString();
  const { data, isPending, isFetching, isError, error } = useQuery({
    queryKey: ["exceptions", query],
    queryFn: () => api.get<ExceptionsResult>(`/api/exceptions?${query}`),
    placeholderData: keepPreviousData,
    staleTime: appConfig.freshness.lists,
    refetchOnWindowFocus: true,
  });

  return (
    <div className="space-y-5">
      <PageHeader
        title="Data exceptions"
        subtitle="Everything in the payer files, claims export or worklog that could not be matched, did not add up, or looked wrong. Each item says what to do."
      />
      <div className="flex flex-col gap-3 md:flex-row md:items-center">
        <div className="md:w-96">
          <SearchInput label="Search exceptions" placeholder="Claim, file name or words in the message" />
        </div>
        <select className="select md:w-56" aria-label="Severity" value={params.get("severity") ?? ""} onChange={(e) => update({ severity: e.target.value, kind: "" })}>
          <option value="">All severities</option>
          {severities.map((severity) => (
            <option key={severity.value} value={severity.value}>{severity.label}</option>
          ))}
        </select>
        <select className="select md:w-72" aria-label="Problem" value={params.get("kind") ?? ""} onChange={(e) => update({ kind: e.target.value })}>
          <option value="">All problems</option>
          {data?.kinds.map((kind) => (
            <option key={kind} value={kind}>{issueLabel(kind)}</option>
          ))}
        </select>
        {data && (
          <p className="text-sm md:ml-auto">
            <strong>{data.totalItems}</strong> exceptions
          </p>
        )}
      </div>
      <section className="card relative overflow-hidden bg-base-100 shadow-sm">
        {isFetching && !isPending && <progress className="progress progress-primary absolute inset-x-0 top-0 h-0.5" aria-label="Refreshing" />}
        {isError && (
          <div className="p-4">
            <ErrorAlert error={error} />
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="table table-sm table-zebra">
            <thead>
              <tr>
                <SortableHeader column="severity" label="Severity" />
                <SortableHeader column="kind" label="Problem" />
                <SortableHeader column="source" label="Found in" />
                <SortableHeader column="claimId" label="Claim" />
                <SortableHeader column="amount" label="Amount" numeric />
              </tr>
            </thead>
            <tbody>
              {isPending && <TableSkeletonRows rows={8} columns={5} />}
              {data?.items.length === 0 && (
                <tr>
                  <td colSpan={5}>
                    <EmptyState title="No exceptions match" hint="Clear the search or change the filters." />
                  </td>
                </tr>
              )}
              {data?.items.map((issue) => (
                <tr key={issue.key}>
                  <td className="w-px"><span className={severityBadge(issue.severity)}>{issue.severity}</span></td>
                  <td className="min-w-80">
                    <div className="font-medium">{issueLabel(issue.kind)}</div>
                    <div>{issue.reason}</div>
                    {issueHint(issue.kind) && <div className="text-xs text-base-content/60">What to do: {issueHint(issue.kind)}</div>}
                  </td>
                  <td className="text-xs break-all text-base-content/70">{issueLocation(issue.source, issue.reference)}</td>
                  <td className="whitespace-nowrap">
                    {issue.claimId ? <Link to={`/claims/${encodeURIComponent(issue.claimId)}`} className="link link-hover link-primary">{issue.claimId}</Link> : "None"}
                  </td>
                  <td className="text-right tabular-nums">{issue.amount === null ? "None" : formatMoney(issue.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {data && <Pagination result={data} />}
      </section>
    </div>
  );
}
