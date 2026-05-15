"use client";

import { Bar, BarChart, CartesianGrid, Tooltip, XAxis, YAxis } from "recharts";

import { useReducedMotion } from "@/hooks/use-reduced-motion";

import { ChartContainer, chartSeriesColor } from "./chart-container";

export interface ChartBarSeries<TData> {
  readonly id: string;
  readonly accessor: keyof TData & string;
  readonly label: string;
}

export interface ChartBarProps<TData> {
  readonly data: ReadonlyArray<TData>;
  readonly xKey: keyof TData & string;
  readonly series: ReadonlyArray<ChartBarSeries<TData>>;
  readonly accessibleLabel: string;
  readonly height?: number;
}

export function ChartBar<TData extends Record<string, unknown>>({
  data,
  xKey,
  series,
  accessibleLabel,
  height,
}: ChartBarProps<TData>) {
  const reducedMotion = useReducedMotion();
  return (
    <ChartContainer accessibleLabel={accessibleLabel} {...(height !== undefined ? { height } : {})}>
      <BarChart data={data as TData[]}>
        <CartesianGrid stroke="var(--color-border-muted)" strokeDasharray="3 3" />
        <XAxis dataKey={xKey as string} stroke="var(--color-foreground-muted)" />
        <YAxis stroke="var(--color-foreground-muted)" />
        <Tooltip
          contentStyle={{
            backgroundColor: "var(--color-surface-overlay)",
            borderColor: "var(--color-border-default)",
            color: "var(--color-foreground-default)",
          }}
        />
        {series.map((s, index) => (
          <Bar
            key={s.id}
            dataKey={s.accessor as string}
            name={s.label}
            fill={chartSeriesColor(index)}
            radius={[4, 4, 0, 0]}
            isAnimationActive={!reducedMotion}
          />
        ))}
      </BarChart>
    </ChartContainer>
  );
}
