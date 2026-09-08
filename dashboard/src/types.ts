/** Shapes mirror the ASP.NET Core API's JSON (camelCase, System.Text.Json defaults). */

export interface CampaignReportRow {
  campaignId: string;
  clicks: number;
  conversions: number;
  revenue: number;
  /** 0..1, rounded to 4 decimal places on the server. */
  conversionRate: number;
}

export interface EventRequest {
  type: "click" | "conversion";
  campaignId: string;
  clickId?: string | null;
  /** Only valid on conversion events; must be >= 0. */
  amount?: number | null;
  /** ISO-8601 UTC; omitted means "now" on the server. */
  occurredAt?: string | null;
  /** Replays with the same key return 200 and the original row. */
  idempotencyKey: string;
}

/** RFC 9457 ProblemDetails as returned by the validation endpoints. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
