'use client';

import { useCallback, useEffect, useState } from 'react';
import { apiClient, ApiError } from '@/lib/api-client';

interface KeysetPage<T> {
  items: T[];
  nextCursor: string | null;
}

export interface UseKeysetListResult<T> {
  items: T[];
  isLoading: boolean;
  error: string | null;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
  nextPage: () => void;
  previousPage: () => void;
  refetch: () => void;
}

/**
 * Task F3. The backend's keyset pages (`KeysetPage<T>`) only carry a forward cursor
 * (`nextCursor`) - there is no server-side "previous". A client-side cursor stack (push
 * the cursor used for the page just left, pop to go back) is the standard technique for
 * a back button over keyset pagination without the server needing to support it.
 */
export function useKeysetList<T>(basePath: string, query: Record<string, string | undefined> = {}): UseKeysetListResult<T> {
  const [items, setItems] = useState<T[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [cursorStack, setCursorStack] = useState<(string | undefined)[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);

  const queryKey = JSON.stringify(query);

  const load = useCallback(
    async (targetCursor: string | undefined) => {
      setIsLoading(true);
      setError(null);

      const params = new URLSearchParams();
      if (targetCursor) {
        params.set('cursor', targetCursor);
      }
      for (const [key, value] of Object.entries(query)) {
        if (value) {
          params.set(key, value);
        }
      }
      const suffix = params.toString();

      try {
        const page = await apiClient.get<KeysetPage<T>>(`${basePath}${suffix ? `?${suffix}` : ''}`);
        setItems(page.items);
        setNextCursor(page.nextCursor);
      } catch (caught) {
        setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل البيانات');
      } finally {
        setIsLoading(false);
      }
    },
    [basePath, queryKey],
  );

  useEffect(() => {
    setCursor(undefined);
    setCursorStack([]);
    void load(undefined);
  }, [basePath, queryKey]);

  function nextPage() {
    if (!nextCursor) {
      return;
    }
    setCursorStack((stack) => [...stack, cursor]);
    setCursor(nextCursor);
    void load(nextCursor);
  }

  function previousPage() {
    if (cursorStack.length === 0) {
      return;
    }
    const stack = [...cursorStack];
    const previous = stack.pop();
    setCursorStack(stack);
    setCursor(previous);
    void load(previous);
  }

  function refetch() {
    void load(cursor);
  }

  return {
    items,
    isLoading,
    error,
    hasNextPage: nextCursor !== null,
    hasPreviousPage: cursorStack.length > 0,
    nextPage,
    previousPage,
    refetch,
  };
}
