import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../components/Toasts";
import { api, errorMessage, orNull } from "../../lib/api";
import { appConfig } from "../../lib/config";
import type { ClaimDetail, DenialDetail, DraftResult, Reference } from "../../lib/types";
import { workKey } from "../work-items/queries";

export const claimKey = (claimId: string) => ["claim", claimId] as const;
export const denialKey = (claimId: string) => ["denial", claimId] as const;

export function useClaim(claimId: string) {
  return useQuery({
    queryKey: claimKey(claimId),
    queryFn: () => api.get<ClaimDetail>(`/api/claims/${encodeURIComponent(claimId)}`),
    staleTime: appConfig.freshness.details,
  });
}

export function useDenial(claimId: string) {
  return useQuery({
    queryKey: denialKey(claimId),
    queryFn: () => orNull(api.get<DenialDetail>(`/api/denials/${encodeURIComponent(claimId)}`)),
    staleTime: appConfig.freshness.details,
  });
}

export function useReference() {
  return useQuery({ queryKey: ["reference"], queryFn: () => api.get<Reference>("/api/reference"), staleTime: appConfig.freshness.reference });
}

export function useDraftLetter(claimId: string) {
  const queryClient = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: () => api.post<DraftResult>(`/api/denials/${encodeURIComponent(claimId)}/draft`),
    onSuccess: (draft) => {
      queryClient.setQueryData<DenialDetail | null>(denialKey(claimId), (current) => (current ? { ...current, draft } : current));
      void queryClient.invalidateQueries({ queryKey: workKey(claimId) });
      toast("success", draft.source === "Gemini" ? "Appeal letter drafted with AI." : "Appeal letter ready.");
    },
    onError: (error) => toast("error", errorMessage(error)),
  });
}
