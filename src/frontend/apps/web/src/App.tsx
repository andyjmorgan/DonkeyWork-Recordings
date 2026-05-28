import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthGuard } from '@/components/AuthGuard';
import { AppLayout } from '@/components/layout/AppLayout';
import { useTokenRefresh } from '@/hooks/useTokenRefresh';
import { ChannelDetailPage } from '@/pages/ChannelDetailPage';
import { ChannelsListPage } from '@/pages/ChannelsListPage';
import { FeedSettingsPage } from '@/pages/FeedSettingsPage';
import { LoginCallbackPage } from '@/pages/LoginCallbackPage';
import { LoginPage } from '@/pages/LoginPage';
import { ProfilePage } from '@/pages/ProfilePage';

export function App() {
  useTokenRefresh();

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/login/callback" element={<LoginCallbackPage />} />
      <Route
        element={
          <AuthGuard>
            <AppLayout />
          </AuthGuard>
        }
      >
        <Route path="/" element={<Navigate to="/channels" replace />} />
        <Route path="/channels" element={<ChannelsListPage />} />
        <Route path="/channels/:id" element={<ChannelDetailPage />} />
        <Route path="/feed-settings" element={<FeedSettingsPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="*" element={<Navigate to="/channels" replace />} />
      </Route>
    </Routes>
  );
}
