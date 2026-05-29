import { Github } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ThemeToggle } from '@/components/layout/ThemeToggle';
import { UserMenu } from '@/components/layout/UserMenu';

const REPO_URL = 'https://github.com/andyjmorgan/DonkeyWork-Recordings';

export function Topbar() {
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-border bg-background px-4 md:px-6">
      <div className="flex-1" />
      <Button variant="ghost" size="icon" className="h-9 w-9" asChild aria-label="View source on GitHub">
        <a href={REPO_URL} target="_blank" rel="noreferrer noopener">
          <Github className="h-5 w-5" />
        </a>
      </Button>
      <ThemeToggle />
      <UserMenu />
    </header>
  );
}
