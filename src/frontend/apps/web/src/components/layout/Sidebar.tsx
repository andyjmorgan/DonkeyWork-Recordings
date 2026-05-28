import { NavLink } from 'react-router-dom';
import { Mic2, Radio, Rss, UserCircle, LogOut } from 'lucide-react';
import { useAuthStore } from '@/store/auth';
import { cn } from '@/lib/utils';

const NAV = [
  { to: '/channels', label: 'Channels', icon: Radio },
  { to: '/feed-settings', label: 'Feed Settings', icon: Rss },
  { to: '/profile', label: 'Profile', icon: UserCircle },
];

export function Sidebar() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const handleLogout = () => {
    const idToken = useAuthStore.getState().idToken;
    logout();
    const hint = idToken ? `?id_token_hint=${encodeURIComponent(idToken)}` : '';
    window.location.assign(`/api/v1/auth/logout${hint}`);
  };

  return (
    <aside className="hidden md:flex md:w-64 md:flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground">
      <div className="flex h-16 items-center gap-3 px-6 border-b border-sidebar-border">
        <div className="inline-flex h-9 w-9 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <Mic2 className="h-5 w-5" />
        </div>
        <div>
          <div className="text-sm font-semibold leading-tight">DonkeyWork</div>
          <div className="text-xs text-muted-foreground">Recordings</div>
        </div>
      </div>

      <nav className="flex-1 px-3 py-4 space-y-1">
        {NAV.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition',
                isActive
                  ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                  : 'text-sidebar-foreground/80 hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
              )
            }
          >
            <Icon className="h-4 w-4" />
            {label}
          </NavLink>
        ))}
      </nav>

      <div className="border-t border-sidebar-border p-3 space-y-2">
        <div className="px-2 text-xs text-muted-foreground truncate">
          {user?.name ?? user?.username ?? user?.email ?? 'unknown'}
        </div>
        <button
          onClick={handleLogout}
          className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm text-sidebar-foreground/80 transition hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground"
        >
          <LogOut className="h-4 w-4" />
          Log out
        </button>
      </div>
    </aside>
  );
}
