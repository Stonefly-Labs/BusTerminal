"use client";

import { Area, AreaChart, CartesianGrid, Tooltip, XAxis, YAxis } from "recharts";

import { useReducedMotion } from "@/hooks/use-reduced-motion";

import { ChartContainer, chartSeriesColor } from "./chart-container";

export interface ChartAreaSeries<TData> {
  readonly id: string;
  readonly accessor: keyof TData & string;
  readonly label: string;
}

export interface ChartAreaProps<TData> {
  readonly data: ReadonlyArray<TData>;
  readonly xKey: keyof TData & string;
  readonly series: ReadonlyArray<ChartAreaSeries<TData>>;
  readonly accessibleLabel: string;
  readonly height?: number;
}

export function ChartArea<TData extends Record<string, unknown>>({
  data,
  xKey,
  series,
  accessibleLabel,
  height,
}: ChartAreaProps<TData>) {
  const reducedMotion = useReducedMotion();
  return (
    <ChartContainer accessibleLabel={accessibleLabel} {...(height !== undefined ? { height } : {})}>
      <AreaChart data={data as TData[]}>
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
          <Area
            key={s.id}
            type="monotone"
            dataKey={s.accessor as string}
            name={s.label}
            stroke={chartSeriesColor(index)}
            fill={chartSeriesColor(index)}
            fillOpacity={0.2}
            isAnimationActive={!reducedMotion}
          />
        ))}
      </AreaChart>
    </ChartContainer>
  );
}
