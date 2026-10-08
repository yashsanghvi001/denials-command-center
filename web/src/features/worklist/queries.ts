import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "../../lib/api";
import { appConfig } from "../../lib/config";
import type { DenialSummary, WorklistPage } from "../../lib/types";
import { useGridParams } from "../../lib/useGridParams";

export function useWorklist() {
  const query = useGridParams().params.toString();
  return useQuery({
    queryKey: ["worklist", query],
    queryFn: () => api.get<WorklistPage>(`/api/worklist?${query}`),
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: true,
  });
}

export function useMoneySummary() {
  return useQuery({ queryKey: ["summary"], queryFn: () => api.get<DenialSummary>("/api/denials/summary"), staleTime: appConfig.freshness.lists });
}
