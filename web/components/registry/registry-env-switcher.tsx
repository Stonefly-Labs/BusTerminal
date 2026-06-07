"use client";

/**
 * Spec 006 / T103b / FR-035. Environment switcher Client Component.
 *
 * - Reads the configured environment list from `GET /api/registry/environments`.
 * - Persists the selection in `localStorage` under `STORAGE_KEY` so the
 *   operator's choice survives reloads.
 * - On first visit (no prior selection), auto-picks the alphabetically-first
 *   environment.
 * - Propagates the selection via URL query (`?environment=<env>`) so RSC
 *   layouts/pages can see it.
 */

import { useEffect, useMemo } from "react";
import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Globe } from "lucide-react";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { listEnvironments, type RegistryApiOptions } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";

const STORAGE_KEY = "busterminal.registry.lastEnvironment";

interface RegistryEnvSwitcherProps {
  readonly apiOptions?: RegistryApiOptions;
}

export function RegistryEnvSwitcher({ apiOptions }: RegistryEnvSwitcherProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const urlEnv = searchParams.get("environment") ?? undefined;

  const environmentsQuery = useQuery({
    queryKey: registryQueryKeys.environments.list(),
    queryFn: () => listEnvironments(apiOptions),
    staleTime: 60_000,
  });

  const environments = useMemo(
    () => [...(environmentsQuery.data ?? [])].sort((a, b) => a.localeCompare(b)),
    [environmentsQuery.data],
  );

  // Resolve effective selection: URL > localStorage > alphabetical-first.
  const effective = useMemo(() => {
    if (urlEnv) return urlEnv;
    if (typeof window !== "undefined") {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (stored && environments.includes(stored)) return stored;
    }
    return environments[0];
  }, [urlEnv, environments]);

  useEffect(() => {
    if (!effective) return;
    if (urlEnv === effective) return;
    const params = new URLSearchParams(searchParams.toString());
    params.set("environment", effective);
    router.replace(`${pathname}?${params.toString()}` as never);
  }, [effective, urlEnv, pathname, router, searchParams]);

  useEffect(() => {
    if (!effective) return;
    if (typeof window === "undefined") return;
    window.localStorage.setItem(STORAGE_KEY, effective);
  }, [effective]);

  if (environmentsQuery.isPending) {
    return (
      <div data-testid="registry-env-switcher-loading" className="text-xs text-foreground-muted">
        Loading environments…
      </div>
    );
  }

  if (environments.length === 0) {
    return null;
  }

  return (
    <div className="flex items-center gap-2" data-testid="registry-env-switcher">
      <Globe className="size-4 text-foreground-muted" aria-hidden="true" />
      <Select
        value={effective ?? ""}
        onValueChange={(next) => {
          const params = new URLSearchParams(searchParams.toString());
          params.set("environment", next);
          router.replace(`${pathname}?${params.toString()}` as never);
          if (typeof window !== "undefined") {
            window.localStorage.setItem(STORAGE_KEY, next);
          }
        }}
      >
        <SelectTrigger className="w-40" aria-label="Environment">
          <SelectValue placeholder="Environment" />
        </SelectTrigger>
        <SelectContent>
          {environments.map((env) => (
            <SelectItem key={env} value={env}>
              {env}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

export const REGISTRY_ENV_STORAGE_KEY = STORAGE_KEY;
