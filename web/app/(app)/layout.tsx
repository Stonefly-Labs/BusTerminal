import type { ReactNode } from "react";
import { Boxes, Inbox, LayoutDashboard, Megaphone } from "lucide-react";

import { AppShell } from "@/components/app-shell/app-shell";
import { Footer } from "@/components/app-shell/footer";
import { Sidebar, SidebarItem } from "@/components/app-shell/sidebar";
import { TopBar } from "@/components/app-shell/top-bar";
import { Toaster } from "@/components/ui/toast";
import { TooltipProvider } from "@/components/ui/tooltip";

interface AppShellLayoutProps {
  readonly children: ReactNode;
}

export default function AppShellLayout({ children }: AppShellLayoutProps) {
  const sidebar = (
    <Sidebar>
      <SidebarItem icon={<LayoutDashboard className="size-4" />} label="Dashboard" active />
      <SidebarItem icon={<Boxes className="size-4" />} label="Namespaces" />
      <SidebarItem icon={<Inbox className="size-4" />} label="Queues" />
      <SidebarItem icon={<Megaphone className="size-4" />} label="Topics" />
    </Sidebar>
  );

  return (
    <TooltipProvider>
      <AppShell
        sidebar={sidebar}
        topBar={<TopBar />}
        main={children}
        footer={<Footer />}
      />
      <Toaster />
    </TooltipProvider>
  );
}
