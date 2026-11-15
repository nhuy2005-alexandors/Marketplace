import { QueryClient } from "@tanstack/react-query";
import { privateQueryPrefixes } from "./sessionIsolation";

export const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
});

export function clearUserScopedQueries(userId: number | null): void {
  for (const queryKey of privateQueryPrefixes(userId)) {
    void queryClient.cancelQueries({ queryKey });
    queryClient.removeQueries({ queryKey });
  }
}
