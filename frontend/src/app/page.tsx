import { redirect } from 'next/navigation';

/**
 * Task F2. `/dashboard` (inside the authenticated route group) is the real landing
 * point; its own client-side guard sends an unauthenticated visitor on to `/login`.
 * A server-side redirect here avoids ever rendering a placeholder root page at all.
 */
export default function RootPage() {
  redirect('/dashboard');
}
