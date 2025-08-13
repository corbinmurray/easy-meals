"use client";

import { Button } from "@workspace/ui/components/button";
import { ArrowLeft, ArrowRight } from "lucide-react";
import { motion } from "motion/react";
import { useState } from "react";

interface WeekNavigationProps {
  currentWeekStart: Date;
  onChangeWeek: (offset: number) => void;
  canGoBack: boolean;
  canGoForward: boolean;
}

export default function WeekNavigation({
  currentWeekStart,
  onChangeWeek,
  canGoBack,
  canGoForward,
}: WeekNavigationProps) {
  const [direction, setDirection] = useState<1 | -1>(1);

  function handleWeekNav(offset: number) {
    setDirection(offset > 0 ? 1 : -1);
    onChangeWeek(offset);
  }

  // Get all days in the current week for display
  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(currentWeekStart);
    d.setDate(currentWeekStart.getDate() + i);
    return d;
  });

  const weekStartFormatted =
    days[0]?.toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
    }) ?? "";

  const weekEndFormatted =
    days[6]?.toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
      year: "numeric",
    }) ?? "";

  return (
    <motion.section
      className="w-full"
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5 }}
    >
      <div className="flex items-center justify-between mb-6">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => handleWeekNav(-1)}
          disabled={!canGoBack}
          aria-label="Previous week"
          className="size-10"
        >
          <ArrowLeft className="size-5 md:size-6" />
        </Button>

        <motion.div
          key={currentWeekStart.toISOString()}
          initial={{ x: direction * 30, opacity: 0 }}
          animate={{ x: 0, opacity: 1 }}
          exit={{ x: -direction * 30, opacity: 0 }}
          transition={{
            type: "spring",
            stiffness: 300,
            damping: 30,
            duration: 0.3,
          }}
          className="text-center"
        >
          <h2 className="text-2xl md:text-3xl font-bold text-foreground">
            {weekStartFormatted} - {weekEndFormatted}
          </h2>
        </motion.div>

        <Button
          variant="ghost"
          size="icon"
          onClick={() => handleWeekNav(1)}
          disabled={!canGoForward}
          aria-label="Next week"
          className="size-10"
        >
          <ArrowRight className="size-5 md:size-6" />
        </Button>
      </div>
    </motion.section>
  );
}
