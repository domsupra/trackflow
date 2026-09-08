import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { EventError, postEvent } from "../api";

interface Props {
  onStored: (message: string) => void;
}

const newKey = () =>
  typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `key-${Date.now()}-${Math.random().toString(16).slice(2)}`;

export default function EventForm({ onStored }: Props) {
  const [type, setType] = useState<"click" | "conversion">("click");
  const [campaignId, setCampaignId] = useState("demo-us");
  const [clickId, setClickId] = useState("");
  const [amount, setAmount] = useState("49.99");
  const [status, setStatus] = useState<{ kind: "ok" | "err"; text: string } | null>(null);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(false);

  // A conversion must reference a click; hand the next form a fresh, valid
  // clickId instead of making the buyer remember the previous one.
  useEffect(() => {
    if (type === "conversion") setClickId(`clk-${Date.now().toString(36)}`);
  }, [type]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setStatus(null);
    setErrors({});
    try {
      const res = await postEvent({
        type,
        campaignId,
        clickId: type === "conversion" ? clickId : null,
        amount: type === "conversion" ? Number(amount) : null,
        idempotencyKey: newKey(),
      });
      const message = res.replayed
        ? "200 — replayed (idempotency key already seen)"
        : "201 — event stored";
      setStatus({ kind: "ok", text: message });
      onStored(message);
    } catch (err) {
      if (err instanceof EventError && err.problems?.errors) {
        setErrors(err.problems.errors);
        setStatus({ kind: "err", text: `${err.status} ${err.problems.title ?? "Unprocessable"}` });
      } else {
        setStatus({ kind: "err", text: err instanceof Error ? err.message : String(err) });
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card">
      <div className="card-head">
        <h2>Send a test event</h2>
        <span className="muted">POST /v1/events</span>
      </div>

      <form onSubmit={submit} className="event-form">
        <label>
          Type
          <select
            value={type}
            onChange={(e) => setType(e.target.value as "click" | "conversion")}
          >
            <option value="click">click</option>
            <option value="conversion">conversion</option>
          </select>
          {errors.type && <span className="field-err">{errors.type[0]}</span>}
        </label>

        <label>
          Campaign ID
          <input
            value={campaignId}
            onChange={(e) => setCampaignId(e.target.value)}
            maxLength={64}
            placeholder="demo-us"
          />
          {errors.campaignId && <span className="field-err">{errors.campaignId[0]}</span>}
        </label>

        {type === "conversion" && (
          <>
            <label>
              Click ID <span className="muted">(required)</span>
              <input value={clickId} onChange={(e) => setClickId(e.target.value)} maxLength={128} />
              {errors.clickId && <span className="field-err">{errors.clickId[0]}</span>}
            </label>

            <label>
              Amount (USD)
              <input
                type="number"
                min="0"
                step="0.01"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
              {errors.amount && <span className="field-err">{errors.amount[0]}</span>}
            </label>
          </>
        )}

        <button type="submit" disabled={busy}>
          {busy ? "Sending…" : "Send event"}
        </button>

        {status && <p className={status.kind === "ok" ? "ok" : "error"}>{status.text}</p>}
      </form>
    </section>
  );
}
