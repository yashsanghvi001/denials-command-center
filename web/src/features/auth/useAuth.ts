import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { api, ApiError } from "../../lib/api";
import type { AuthOptions, CurrentUser } from "../../lib/types";

export const meKey = ["me"] as const;

async function fetchCurrentUser(): Promise<CurrentUser | null> {
  try {
    return await api.get<CurrentUser>("/api/auth/me");
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

// Keeps the mounted ["me"] query (and its observer) alive while discarding everything cached for the previous user.
const dropOtherQueries = (queryClient: QueryClient) =>
  queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== meKey[0] });

export function useCurrentUser() {
  return useQuery({ queryKey: meKey, queryFn: fetchCurrentUser, staleTime: Infinity, retry: false });
}

export function useSignedInUser(): CurrentUser {
  const { data } = useCurrentUser();
  if (!data) throw new Error("useSignedInUser is only available on signed-in pages.");
  return data;
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (credentials: { username: string; password: string }) => api.post<CurrentUser>("/api/auth/login", credentials),
    onSuccess: (user) => {
      queryClient.setQueryData(meKey, user);
      dropOtherQueries(queryClient);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>("/api/auth/logout"),
    onSettled: () => {
      queryClient.setQueryData(meKey, null);
      dropOtherQueries(queryClient);
    },
  });
}

export function useAuthOptions() {
  return useQuery({ queryKey: ["auth-options"], queryFn: () => api.get<AuthOptions>("/api/auth/options"), staleTime: Infinity });
}
