"use client";

import RecipeGrid from "@/components/WeeklyMealSelector/RecipeGrid";
import SelectedRecipesList from "@/components/WeeklyMealSelector/SelectedRecipesList";
import WeekNavigation from "@/components/WeeklyMealSelector/WeekNavigation";
import type { Recipe, WeeklyMealPlanState } from "@/types/meal-planner";
import { useState } from "react";

interface WeeklyMealSelectorProps {
  recipes: Recipe[];
}

function getStartOfWeek(date: Date): Date {
  const d = new Date(date);
  d.setDate(d.getDate() - d.getDay());
  d.setHours(0, 0, 0, 0);
  return d;
}

function getWeekKey(date: Date): string {
  return getStartOfWeek(date).toISOString().split("T")[0]!;
}

export default function WeeklyMealSelector({
  recipes,
}: WeeklyMealSelectorProps) {
  const [currentWeekStart, setCurrentWeekStart] = useState<Date>(
    getStartOfWeek(new Date())
  );
  const [weeklyPlans, setWeeklyPlans] = useState<WeeklyMealPlanState>({});

  const currentWeekKey = getWeekKey(currentWeekStart);
  const currentWeekMeals = weeklyPlans[currentWeekKey] || [];

  // Navigation bounds: ±1 year from today
  const today = new Date();
  const minDate = getStartOfWeek(
    new Date(today.getFullYear() - 1, today.getMonth(), today.getDate())
  );
  const maxDate = getStartOfWeek(
    new Date(today.getFullYear() + 1, today.getMonth(), today.getDate())
  );

  function changeWeek(offset: number) {
    setCurrentWeekStart((prev) => {
      const next = new Date(prev);
      next.setDate(next.getDate() + offset * 7);
      if (next < minDate) return minDate;
      if (next > maxDate) return maxDate;
      return next;
    });
  }

  function handleRecipeSelect(recipeId: string) {
    setWeeklyPlans((prev) => {
      const currentMeals = prev[currentWeekKey] || [];
      const isSelected = currentMeals.some(
        (meal) => meal.recipeId === recipeId
      );

      if (isSelected) {
        // Remove recipe
        return {
          ...prev,
          [currentWeekKey]: currentMeals.filter(
            (meal) => meal.recipeId !== recipeId
          ),
        };
      } else if (currentMeals.length < 7) {
        // Add recipe (up to 7 per week)
        return {
          ...prev,
          [currentWeekKey]: [...currentMeals, { recipeId }],
        };
      }

      return prev;
    });
  }

  function handleRecipeRemove(recipeId: string) {
    setWeeklyPlans((prev) => ({
      ...prev,
      [currentWeekKey]: (prev[currentWeekKey] || []).filter(
        (meal) => meal.recipeId !== recipeId
      ),
    }));
  }

  function handleClearWeek() {
    setWeeklyPlans((prev) => ({
      ...prev,
      [currentWeekKey]: [],
    }));
  }

  const isRecipeSelected = (recipeId: string) =>
    currentWeekMeals.some((meal) => meal.recipeId === recipeId);

  const selectedRecipes = currentWeekMeals
    .map((meal) => recipes.find((recipe) => recipe.id === meal.recipeId))
    .filter(Boolean) as Recipe[];

  return (
    <div className="w-full max-w-6xl mx-auto space-y-8">
      {/* Week Navigation */}
      <WeekNavigation
        currentWeekStart={currentWeekStart}
        onChangeWeek={changeWeek}
        canGoBack={currentWeekStart > minDate}
        canGoForward={currentWeekStart < maxDate}
      />

      {/* Selected Recipes Display */}
      <SelectedRecipesList
        selectedRecipes={selectedRecipes}
        maxRecipes={7}
        onRemoveRecipe={handleRecipeRemove}
        onClearAll={handleClearWeek}
      />

      {/* Recipe Grid */}
      <RecipeGrid
        recipes={recipes}
        isRecipeSelected={isRecipeSelected}
        onRecipeSelect={handleRecipeSelect}
        maxSelectionsReached={currentWeekMeals.length >= 7}
      />
    </div>
  );
}
