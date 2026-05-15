"use client";

import * as React from "react";
import { CartesianGrid, Line, LineChart, Tooltip, XAxis, YAxis } from "recharts";

import { ChartContainer, chartSeriesColor } from "./chart-container";

export interface ChartLineSeries<TData> {
  readonly id: string;
  readonly accessor: keyof TData & string;
  readonly label: string;
}

export interface ChartLineProps<TData> {
  readonly data: ReadonlyArray<TData>;
  readonly xKey: keyof TData & string;
  readonly series: ReadonlyArray<ChartLineSeries<TData>>;
  readonly accessibleLabel: string;
  readonly height?: number;
}

export function ChartLine<TData extends Record<string, unknown>>({
  data,
  xKey,
  series,
  accessibleLabel,
  height,
}: ChartLineProps<TData>) {
  return (
    <ChartContainer accessibleLabel={accessibleLabel} {...(height !== undefined ? { height } : {})}>
      <LineChart data={data as TData[]}>
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
          <Line
            key={s.id}
            type="monotone"
            dataKey={s.accessor as string}
            name={s.label}
            stroke={chartSeriesColor(index)}
            strokeWidth={2}
            dot={false}
          />
        ))}
      </LineChart>
    </ChartContainer>
  );
}
