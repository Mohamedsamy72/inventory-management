/**
 * Task F2. Public routes (login, forgot-password) get a plain centered canvas - no
 * sidebar, no session fetch, no auth guard. Deliberately does NOT mount
 * `SessionProvider`: that provider's `/account/me` fetch redirects to `/login` on a
 * 401 (api-client's own behaviour), which would loop if it ever ran on this very page.
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return <div className="flex min-h-dvh items-center justify-center bg-muted p-4">{children}</div>;
}
