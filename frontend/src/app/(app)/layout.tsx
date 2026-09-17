import { SessionProvider } from '@/lib/auth/session-context';
import { AuthenticatedShell } from './authenticated-shell';

/** Task F2. Every route under this group requires a session; {@link AuthenticatedShell}
 * (a client component) owns the actual loading/redirect/render decision. */
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <SessionProvider>
      <AuthenticatedShell>{children}</AuthenticatedShell>
    </SessionProvider>
  );
}
