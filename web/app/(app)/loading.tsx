import { Skeleton } from "@/components/ui/skeleton";
import { t } from "@/lib/i18n";

export default function AppLoading() {
  return (
    <div className="mx-auto flex w-full max-w-screen-2xl flex-col gap-4 p-6" aria-busy="true">
      <span className="sr-only">{t("a11y.loading")}</span>
      <Skeleton className="h-7 w-1/3" />
      <Skeleton className="h-4 w-1/4" />
      <div className="mt-4 flex flex-col gap-2">
        {Array.from({ length: 6 }).map((_, index) => (
          <Skeleton key={index} className="h-10 w-full" />
        ))}
      </div>
    </div>
  );
}
