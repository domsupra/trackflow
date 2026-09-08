import type { CampaignReportRow, EventRequest, ProblemDetails } from "./types";

/** Per-campaign totals for [from, to). Window is half-open, as in the API. */
export async function fetchCampaignReport(from: Date, to: Date): Promise<CampaignReportRow[]> {
  const params = new URLSearchParams({ from: from.toISOString(), to: to.toISOString() });
  const res = await fetch(`/v1/reports/campaigns?${params.toString()}`);
  if (!res.ok) throw new Error(`Reports request failed (${res.status})`);
  return (await res.json()) as CampaignReportRow[];
}

export class EventError extends Error {
  readonly status: number;
  readonly problems: ProblemDetails | null;

  constructor(status: number, problems: ProblemDetails | null) {
    super(`API responded ${status}`);
    this.name = "EventError";
    this.status = status;
    this.problems = problems;
  }
}

export interface PostEventResult {
  status: number;
  /** 200 means the idempotency key was a replay; 201 means stored. */
  replayed: boolean;
}

export async function postEvent(body: EventRequest): Promise<PostEventResult> {
  const res = await fetch("/v1/events", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  let problems: ProblemDetails | null = null;
  if (!res.ok) {
    const contentType = res.headers.get("content-type") ?? "";
    if (contentType.includes("json")) problems = (await res.json()) as ProblemDetails;
    throw new EventError(res.status, problems);
  }
  return { status: res.status, replayed: res.status === 200 };
}
