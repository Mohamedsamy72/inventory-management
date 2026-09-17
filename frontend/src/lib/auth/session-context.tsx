'use client';

import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { apiClient, ApiError } from '@/lib/api-client';
import type { AccountProfile } from './types';

interface SessionState {
  profile: AccountProfile | null;
  isLoading: boolean;
  error: string | null;
  /** Re-fetches `/account/me` - call after login and after any scope/role change. */
  refetch: () => Promise<void>;
}

const SessionContext = createContext<SessionState | null>(null);

/**
 * Task F2 ("session bootstrap via /account/me"). One fetch, shared through context, so
 * every consumer (nav, role badge, route guards) reads the SAME profile rather than
 * each firing its own request.
 *
 * Mount this ONLY inside the authenticated `(app)` route group's layout, never at the
 * root layout - `api-client`'s `request()` already redirects to `/login` on any 401,
 * which is exactly the desired behaviour for a protected route whose session turned
 * out to be invalid, but would loop if this provider (and therefore this fetch) were
 * also mounted ON `/login` itself.
 */
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [profile, setProfile] = useState<AccountProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refetch = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const result = await apiClient.get<AccountProfile>('/api/v1/account/me');
      setProfile(result);
    } catch (caught) {
      setProfile(null);
      // A 401 means api-client is already navigating to /login (see the class-level
      // comment) - render nothing rather than flashing an error banner during that
      // navigation. Anything else is a genuine failure worth surfacing.
      if (!(caught instanceof ApiError) || caught.status !== 401) {
        setError(caught instanceof ApiError ? caught.messageAr : 'تعذر تحميل بيانات الجلسة');
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  return <SessionContext.Provider value={{ profile, isLoading, error, refetch }}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionState {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error('useSession must be used within a SessionProvider');
  }

  return context;
}
