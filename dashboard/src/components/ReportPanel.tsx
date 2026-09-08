import { useEffect, useState } from "react";
import { fetchCampaignReport } from "../api";
import type { CampaignReportRow } from "../types";

interface Props {
  /** Bumped by the parent after each event is stored, to refetch. */
  refreshKey: number;
}

interface Totals {
  clicks: number;
  conversions: number;
  revenue: number;
}

const currency = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export default function ReportPanel({ refreshKey }: Props) {
  const [rows, setRows] = useState<CampaignReportRow[]>([]);
  const [windowLabel, setWindowLabel] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Rolling last-24-hours window: [now - 24h, now), refreshed on every render of a new key.
    const to = new Date();
    const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
    setWindowLabel(`${from.toISOString().slice(0, 16).replace("T", " ")} – ${to.toISOString().slice(0, 16).replace("T", " ")} UTC`);

    setLoading(true);
    setError(null);
    fetchCampaignReport(from, to)
      .then(setRows)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setLoading(false));
  }, [refreshKey]);

  const totals: Totals = rows.reduce(
    (acc, r) => ({
      clicks: acc.clicks + r.clicks,
      conversions: acc.conversions + r.conversions,
      revenue: acc.revenue + r.revenue,
    }),
    { clicks: 0, conversions: 0, revenue: 0 },
  );
  const overallRate = totals.clicks === 0 ? "–" : `${((totals.conversions / totals.clicks) * 100).toFixed(1)}%`;

  return (
    <section className="card">
      <div className="card-head">
        <h2>Per-campaign totals — last 24 h</h2>
        <span className="muted">{windowLabel}</span>
      </div>

      {loading && <p className="muted">Loading…</p>}
      {error && <p className="error">Failed to load report: {error}</p>}
      {!loading && !error && rows.length === 0 && (
        <p className="muted">No events in this window. Send some from the form on the right.</p>
      )}

      {!loading && !error && rows.length > 0 && (
        <table className="report">
          <thead>
            <tr>
              <th>Campaign</th>
              <th className="num">Clicks</th>
              <th className="num">Conversions</th>
              <th className="num">Revenue</th>
              <th className="num">Conv. rate</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.campaignId}>
                <td className="mono">{r.campaignId}</td>
                <td className="num">{r.clicks}</td>
                <td className="num">{r.conversions}</td>
                <td className="num">{currency.format(r.revenue)}</td>
                <td className="num">{(r.conversionRate * 100).toFixed(1)}%</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="totals">
              <td>Total</td>
              <td className="num">{totals.clicks}</td>
              <td className="num">{totals.conversions}</td>
              <td className="num">{currency.format(totals.revenue)}</td>
              <td className="num">{overallRate}</td>
            </tr>
          </tfoot>
        </table>
      )}
    </section>
  );
}
