import type { MouseEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { EmptyState, ErrorAlert } from "../../components/Feedback";
import { Pagination } from "../../components/grid/Pagination";
import { SortableHeader } from "../../components/grid/SortableHeader";
import { TableSkeletonRows } from "../../components/Skeletons";
import { bucketBadge, daysLeftText, formatMoney, splitWords } from "../../lib/format";
import { bucketLabel, workStatusLabel } from "../../lib/labels";
import { useSignedInUser } from "../auth/useAuth";
import { AssigneeSelect } from "../work-items/AssigneeSelect";
import { useWorklist } from "./queries";

export function WorklistTable() {
  const isManager = useSignedInUser().role === "Manager";
  const { data, isPending, isFetching, isError, error } = useWorklist();
  const navigate = useNavigate();
  const { search } = useLocation();
  const columns = isManager ? 12 : 11;

  function openClaim(event: MouseEvent<HTMLTableRowElement>, claimId: string) {
    if ((event.target as HTMLElement).closest("a, button, select, input, label")) return;
    navigate(`/claims/${encodeURIComponent(claimId)}`, { state: { from: search } });
  }

  return (
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
              <SortableHeader column="claimId" label="Claim" />
              <SortableHeader column="patientName" label="Patient" />
              <SortableHeader column="payer" label="Payer" />
              <SortableHeader column="deniedAmount" label="Denied" numeric />
              <SortableHeader column="bucket" label="Outlook" />
              <SortableHeader column="daysToDeadline" label="Deadline" />
              <SortableHeader column="expectedValue" label="Expected back" numeric />
              <SortableHeader column="priorityScore" label="Priority" numeric />
              <SortableHeader column="rootCause" label="Why denied" />
              <th scope="col">Next step</th>
              {isManager && <SortableHeader column="assignedTo" label="Assigned to" />}
              <SortableHeader column="status" label="Work status" />
            </tr>
          </thead>
          <tbody>
            {isPending && <TableSkeletonRows rows={10} columns={columns} />}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={columns}>
                  <EmptyState title="No claims match" hint="Clear the search or change the filters." />
                </td>
              </tr>
            )}
            {data?.items.map((row) => (
              <tr key={row.claimId} className="cursor-pointer hover:bg-base-200" onClick={(e) => openClaim(e, row.claimId)}>
                <td className="whitespace-nowrap">
                  <Link to={`/claims/${encodeURIComponent(row.claimId)}`} state={{ from: search }} className="link link-hover link-primary font-medium">{row.claimId}</Link>
                </td>
                <td className="whitespace-nowrap">{row.patientName}</td>
                <td>{row.payer}</td>
                <td className="text-right tabular-nums">{formatMoney(row.deniedAmount)}</td>
                <td><span className={bucketBadge(row.bucket)}>{bucketLabel(row.bucket)}</span></td>
                <td className={`whitespace-nowrap ${row.daysToDeadline !== null && row.daysToDeadline < 0 ? "text-error" : ""}`}>{daysLeftText(row.daysToDeadline)}</td>
                <td className="text-right tabular-nums">{formatMoney(row.expectedValue)}</td>
                <td className="text-right tabular-nums">{row.priorityScore.toFixed(0)}</td>
                <td>
                  {row.rootCause}
                  {row.confidence === "Low" && <span className="badge badge-soft badge-warning badge-sm ml-1">Needs review</span>}
                </td>
                <td>{splitWords(row.action)}</td>
                {isManager && (
                  <td>
                    <AssigneeSelect claimId={row.claimId} assignedTo={row.assignedTo} />
                  </td>
                )}
                <td className="whitespace-nowrap">{workStatusLabel(row.status)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data && <Pagination result={data} />}
    </section>
  );
}
