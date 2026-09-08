import { useEffect, useState } from "react";
import ReportPanel from "./components/ReportPanel";
import EventForm from "./components/EventForm";

type BackendState = "checking" | "up" | "down";

function useBackendStatus(): BackendState {
  const [state, setState] = useState<BackendState>("checking");
  useEffect(() => {
    let cancelled = false;
    async function check() {
      try {
        const res = await fetch("/health");
        if (!cancelled) setState(res.ok ? "up" : "down");
      } catch {
        if (!cancelled) setState("down");
      }
    }
    check();
    const timer = setInterval(check, 15000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);
  return state;
}

export default function App() {
  const backend = useBackendStatus();
  // Bumping this key makes ReportPanel refetch; it goes up after every stored event.
  const [refreshKey, setRefreshKey] = useState(0);

  return (
    <div className="page">
      <header className="page-head">
        <div>
          <h1>
            TrackFlow <span className="tag">API + dashboard</span>
          </h1>
          <p className="muted">
            Campaign event tracking: idempotent capture, validation, and live per-campaign
            reporting — ASP.NET Core, EF Core, SQLite, with this dashboard in React +
            TypeScript + Vite.
          </p>
        </div>
        <span className={`pill pill-${backend}`} title="GET /health">
          {backend === "checking" ? "…" : backend}
        </span>
      </header>

      <main className="grid">
        <ReportPanel refreshKey={refreshKey} />
        <EventForm onStored={() => setRefreshKey((k) => k + 1)} />
      </main>

      <footer className="muted small">
        One process serves API and UI. Reports show the rolling last 24 hours and refresh
        automatically after each event you send.
      </footer>
    </div>
  );
}
