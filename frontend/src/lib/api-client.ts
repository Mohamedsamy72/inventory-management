/**
 * Task F1.11. The ONLY place in the frontend that calls `fetch` against the backend.
 * `credentials: 'include'` on every request (the session lives in an httpOnly cookie);
 * `X-CSRF-TOKEN` attached automatically to every mutating request; RFC 7807 responses
 * parsed into {@link ApiError}, surfacing `messageAr` (never `messageEn` - docs/31
 * section 4.1 rule 13, and Production omits it entirely - CR-072); a 401 redirects to
 * `/login`. No browser storage of any token, anywhere - the CSRF token is held only in
 * this module's in-memory variable and re-fetched fresh for the mutating request that
 * needs it, exactly matching how the session cookie itself is never readable from
 * script.
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5165';

/** A policy denial (403) or a bare 404 carries no problem body - say what it means instead of 'unexpected error'. */
function fallbackMessageFor(status: number): string {
  if (status === 403) return 'ليس لديك صلاحية لتنفيذ هذا الإجراء. إذا سجّلت الدخول بحساب آخر في نفس المتصفح فأعد تسجيل الدخول بحساب المالك.';
  if (status === 404) return 'العنصر المطلوب غير موجود.';
  return 'حدث خطأ غير متوقع، يرجى المحاولة لاحقاً';
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    public readonly messageAr: string,
    public readonly correlationId: string,
  ) {
    super(messageAr);
    this.name = 'ApiError';
  }
}

interface ProblemDetails {
  code?: string;
  messageAr?: string;
  correlationId?: string;
}

async function fetchCsrfToken(): Promise<string> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/csrf-token`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new ApiError(response.status, 'CSRF_TOKEN_UNAVAILABLE', 'تعذر تجهيز الطلب، يرجى تحديث الصفحة', '');
  }

  const body = (await response.json()) as { csrfToken: string };
  return body.csrfToken;
}

function isMutatingMethod(method: string): boolean {
  return method !== 'GET' && method !== 'HEAD';
}

async function request<T>(path: string, init: RequestInit = {}, skipAuthRedirect = false): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase();
  const headers = new Headers(init.headers);

  if (isMutatingMethod(method)) {
    headers.set('X-CSRF-TOKEN', await fetchCsrfToken());
  }

  if (init.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    method,
    headers,
    credentials: 'include',
  });

  if (response.status === 401) {
    // A 401 from the login endpoint itself is "wrong credentials", not "session expired" -
    // the caller (LoginPage) renders its own inline error and must not be yanked away by a
    // hard navigation before it gets the chance to.
    if (!skipAuthRedirect && typeof window !== 'undefined') {
      window.location.assign('/login');
    }

    const problem: ProblemDetails | null = await response.json().catch(() => null);
    throw new ApiError(
      401,
      problem?.code ?? 'UNAUTHENTICATED',
      problem?.messageAr ?? 'يجب تسجيل الدخول للمتابعة',
      problem?.correlationId ?? '',
    );
  }

  if (!response.ok) {
    const problem: ProblemDetails | null = await response.json().catch(() => null);
    throw new ApiError(
      response.status,
      problem?.code ?? 'UNKNOWN_ERROR',
      problem?.messageAr ?? fallbackMessageFor(response.status),
      problem?.correlationId ?? '',
    );
  }

  // 204 is the documented empty-body status, but several endpoints (e.g. login: `Results.Ok()`
  // with no value) return 200 with an equally empty body - `.json()` on that throws a raw
  // SyntaxError instead of the intended success, so check for actual content first rather than
  // trusting the status code alone.
  const text = await response.text();
  if (text === '') {
    return undefined as T;
  }

  return JSON.parse(text) as T;
}

export interface RequestOptions {
  /** e.g. a per-form-instance idempotency key (guide section 8.4) - never regenerated on retry. */
  headers?: HeadersInit;
  signal?: AbortSignal;
  /** True for pre-auth calls (login) whose own 401 is "wrong credentials", not "session expired". */
  skipAuthRedirect?: boolean;
}

export const apiClient = {
  get: <T>(path: string, options?: RequestOptions): Promise<T> => request<T>(path, options, options?.skipAuthRedirect),

  post: <T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> =>
    request<T>(
      path,
      { ...options, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) },
      options?.skipAuthRedirect,
    ),

  put: <T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> =>
    request<T>(
      path,
      { ...options, method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) },
      options?.skipAuthRedirect,
    ),

  delete: <T>(path: string, options?: RequestOptions): Promise<T> =>
    request<T>(path, { ...options, method: 'DELETE' }, options?.skipAuthRedirect),
};
