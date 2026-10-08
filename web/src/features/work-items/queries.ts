import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../components/Toasts";
import { api, errorMessage, orNull } from "../../lib/api";
import { appConfig } from "../../lib/config";
import type { Specialist, WorkItem, WorkItemDetail, WorkNote } from "../../lib/types";

export const workKey = (claimId: string) => ["work", claimId] as const;

const workPath = (claimId: string) => `/api/worklist/${encodeURIComponent(claimId)}`;

export function useWorkItem(claimId: string) {
  return useQuery({ queryKey: workKey(claimId), queryFn: () => orNull(api.get<WorkItemDetail>(workPath(claimId))), staleTime: appConfig.freshness.work });
}

export function useSpecialists(enabled: boolean) {
  return useQuery({ queryKey: ["users"], queryFn: () => api.get<Specialist[]>("/api/users"), enabled, staleTime: appConfig.freshness.reference });
}

function useWorkMutation<Variables extends { claimId: string }, Result>(
  send: (variables: Variables) => Promise<Result>,
  successMessage: string,
  affectsWorklist: boolean,
) {
  const queryClient = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: send,
    onSuccess: (_, { claimId }) => {
      toast("success", successMessage);
      // Returned so the mutation stays pending until the refetched data arrives.
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: workKey(claimId) }),
        affectsWorklist ? queryClient.invalidateQueries({ queryKey: ["worklist"] }) : undefined,
      ]);
    },
    onError: (error) => toast("error", errorMessage(error)),
  });
}

export const useAssign = () =>
  useWorkMutation(
    ({ claimId, username }: { claimId: string; username: string | null }) => api.put<WorkItem>(`${workPath(claimId)}/assignee`, { username }),
    "Assignment saved.",
    true,
  );

export const useChangeStatus = () =>
  useWorkMutation(
    ({ claimId, status }: { claimId: string; status: string }) => api.patch<WorkItem>(`${workPath(claimId)}/status`, { status }),
    "Work status updated.",
    true,
  );

export const useAddNote = () =>
  useWorkMutation(({ claimId, text }: { claimId: string; text: string }) => api.post<WorkNote>(`${workPath(claimId)}/notes`, { text }), "Note saved.", false);
