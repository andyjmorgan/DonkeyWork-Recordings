import { Outlet } from 'react-router-dom';
import { Sidebar } from '@/components/layout/Sidebar';

export function AppLayout() {
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <Sidebar />
      <main className="flex-1 min-w-0">
        <Outlet />
      </main>
    </div>
  );
}
