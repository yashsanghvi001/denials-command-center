import { useAssign, useSpecialists } from "./queries";

export function AssigneeSelect({ claimId, assignedTo, size = "sm" }: { claimId: string; assignedTo: string | null; size?: "sm" | "md" }) {
  const specialists = useSpecialists(true);
  const assign = useAssign();

  return (
    <select
      aria-label={`Assign ${claimId}`}
      className={`select w-full min-w-36 ${size === "sm" ? "select-sm" : ""}`}
      value={assignedTo ?? ""}
      disabled={assign.isPending || specialists.isPending}
      onChange={(e) => assign.mutate({ claimId, username: e.target.value || null })}
    >
      <option value="">Nobody yet</option>
      {specialists.data?.map((specialist) => (
        <option key={specialist.username} value={specialist.username}>{specialist.displayName}</option>
      ))}
    </select>
  );
}
