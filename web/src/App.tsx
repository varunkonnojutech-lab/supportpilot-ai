import { useCallback, useEffect, useState } from "react";
import { api, clearToken, getToken, type CurrentUser } from "./api";
import Landing from "./pages/Landing";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Dashboard from "./pages/Dashboard";

export type Navigate = (path: string) => void;

function currentPath(): string {
  return window.location.pathname;
}

export default function App() {
  const [path, setPath] = useState<string>(currentPath);
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [authChecked, setAuthChecked] = useState<boolean>(!getToken());

  const navigate = useCallback<Navigate>((next: string) => {
    if (next !== window.location.pathname) {
      window.history.pushState(null, "", next);
    }
    setPath(next);
    window.scrollTo(0, 0);
  }, []);

  useEffect(() => {
    const onPopState = () => setPath(currentPath());
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  // Restore the session on first load when a token is stored.
  useEffect(() => {
    if (!getToken()) return;
    let cancelled = false;
    api
      .me()
      .then((me) => {
        if (!cancelled) setUser(me);
      })
      .catch(() => {
        if (!cancelled) {
          clearToken();
          setUser(null);
        }
      })
      .finally(() => {
        if (!cancelled) setAuthChecked(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleAuthed = useCallback(
    (me: CurrentUser) => {
      setUser(me);
      setAuthChecked(true);
      navigate("/dashboard");
    },
    [navigate],
  );

  const handleLogout = useCallback(() => {
    clearToken();
    setUser(null);
    navigate("/");
  }, [navigate]);

  if (!authChecked) {
    return (
      <div className="auth-wrap">
        <p className="empty">Loading SupportPilot AI…</p>
      </div>
    );
  }

  if (path === "/login") {
    if (user) {
      return (
        <Dashboard user={user} navigate={navigate} onLogout={handleLogout} />
      );
    }
    return <Login navigate={navigate} onAuthed={handleAuthed} />;
  }

  if (path === "/register") {
    if (user) {
      return (
        <Dashboard user={user} navigate={navigate} onLogout={handleLogout} />
      );
    }
    return <Register navigate={navigate} onAuthed={handleAuthed} />;
  }

  if (path === "/dashboard" || path.startsWith("/dashboard/")) {
    if (!user) {
      return <Login navigate={navigate} onAuthed={handleAuthed} />;
    }
    return <Dashboard user={user} navigate={navigate} onLogout={handleLogout} />;
  }

  // "/" (and anything unknown) is the public landing page; signed-in users
  // get a shortcut into the dashboard from the nav.
  return <Landing navigate={navigate} user={user} />;
}
