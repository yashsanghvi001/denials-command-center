import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "../lib/api";
import { appConfig } from "../lib/config";

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: appConfig.freshness.default,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
    mutations: { retry: false },
  },
});
