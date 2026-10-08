import { useState, type FormEvent } from "react";
import { EmptyState, ErrorAlert } from "../../../components/Feedback";
import { SectionCard } from "../../../components/SectionCard";
import { DetailSkeleton } from "../../../components/Skeletons";
import { appConfig } from "../../../lib/config";
import { formatDateTime } from "../../../lib/format";
import { workStatusLabel } from "../../../lib/labels";
import { useSignedInUser } from "../../auth/useAuth";
import { AssigneeSelect } from "../../work-items/AssigneeSelect";
import { useAddNote, useChangeStatus, useWorkItem } from "../../work-items/queries";
import { personName } from "../claimText";

const workStatuses = ["Open", "InProgress", "PendingPayer", "Closed"];

export function WorkTab({ claimId }: { claimId: string }) {
  const isManager = useSignedInUser().role === "Manager";
  const work = useWorkItem(claimId);
  const changeStatus = useChangeStatus();
  const addNote = useAddNote();
  const [note, setNote] = useState("");

  if (work.isPending) return <SectionCard title="Work"><DetailSkeleton lines={5} /></SectionCard>;
  if (work.isError) return <ErrorAlert error={work.error} />;
  if (!work.data) {
    return (
      <SectionCard title="Work">
        <EmptyState title="Not on the worklist" hint="Only denied claims are worked here." />
      </SectionCard>
    );
  }

  const { item, notes } = work.data;

  function submitNote(event: FormEvent) {
    event.preventDefault();
    addNote.mutate({ claimId, text: note }, { onSuccess: () => setNote("") });
  }

  return (
    <div className="grid gap-5 lg:grid-cols-3">
      <SectionCard title="Work">
        <div className="space-y-4">
          <div className="fieldset p-0">
            <label htmlFor="work-status" className="fieldset-legend pt-0">Work status</label>
            <select
              id="work-status"
              className="select w-full"
              value={item.status}
              disabled={changeStatus.isPending}
              onChange={(e) => changeStatus.mutate({ claimId, status: e.target.value })}
            >
              {workStatuses.map((status) => (
                <option key={status} value={status}>{workStatusLabel(status)}</option>
              ))}
            </select>
          </div>
          <div className="fieldset p-0">
            <span className="fieldset-legend pt-0">Assigned to</span>
            {isManager ? <AssigneeSelect claimId={claimId} assignedTo={item.assignedTo} size="md" /> : <p className="text-sm">{personName(item.assignedTo)}</p>}
          </div>
          <p className="text-xs text-base-content/60">
            Last changed {formatDateTime(item.updatedAt)} by {personName(item.updatedBy)}
          </p>
        </div>
      </SectionCard>
      <div className="lg:col-span-2">
        <SectionCard title={`Notes (${notes.length})`}>
          <div className="space-y-4">
            {notes.length === 0 && <p className="text-sm text-base-content/60">No notes yet.</p>}
            <ul className="space-y-3">
              {notes.map((n) => (
                <li key={n.id} className="rounded-box bg-base-200 p-3">
                  <div className="text-xs text-base-content/60">
                    <strong className="text-base-content">{personName(n.author)}</strong> · {formatDateTime(n.createdAt)}
                  </div>
                  <p className="mt-1 whitespace-pre-wrap text-sm">{n.text}</p>
                </li>
              ))}
            </ul>
            <form onSubmit={submitNote} className="space-y-2">
              <label htmlFor="new-note" className="sr-only">Add a note</label>
              <textarea
                id="new-note"
                className="textarea w-full"
                rows={3}
                maxLength={appConfig.maxNoteLength}
                placeholder="e.g. Called the payer, reference 12345"
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
              <button type="submit" className="btn btn-primary btn-sm" disabled={addNote.isPending || note.trim() === ""}>
                {addNote.isPending && <span className="loading loading-spinner loading-xs" />}
                Save note
              </button>
            </form>
          </div>
        </SectionCard>
      </div>
    </div>
  );
}
