"use client";

import RecipeCard from "@/components/RecipeCard";
import type { Recipe } from "@/types/meal-planner";
import { motion } from "motion/react";
import { useEffect, useMemo, useState } from "react";

interface RecipeGridProps {
  recipes: Recipe[];
  isRecipeSelected: (recipeId: string) => boolean;
  onRecipeSelect: (recipeId: string) => void;
  maxSelectionsReached: boolean;
  // New props for enhanced functionality
  currentWeekStart?: Date; // For week-based randomization
  recipesPerPage?: number; // For pagination (default: 12)
  enableSearch?: boolean; // Enable/disable search (default: true)
  enableTagFiltering?: boolean; // Enable/disable tag filtering (default: true)
  enablePagination?: boolean; // Enable/disable pagination (default: true)
}

export default function RecipeGrid({
  recipes,
  isRecipeSelected,
  onRecipeSelect,
  maxSelectionsReached,
  currentWeekStart,
  recipesPerPage = 12,
  enableSearch = true,
  enableTagFiltering = true,
  enablePagination = true,
}: RecipeGridProps) {
  // State for search functionality
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState("");

  // State for tag filtering
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  // State for pagination
  const [currentPage, setCurrentPage] = useState(1);

  // Debounce search term
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearchTerm(searchTerm);
    }, 300);

    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Reset page when search or filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [debouncedSearchTerm, selectedTags]);

  // Extract unique tags from all recipes
  const availableTags = useMemo(() => {
    const tagSet = new Set<string>();
    recipes.forEach((recipe) => {
      recipe.tags?.forEach((tag) => tagSet.add(tag));
    });
    return Array.from(tagSet).sort();
  }, [recipes]);
  return (
    <motion.section
      className="w-full"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, delay: 0.2 }}
    >
      <div className="mb-6">
        <h3 className="text-xl font-semibold mb-2">Available Recipes</h3>
        <p className="text-muted-foreground">
          {maxSelectionsReached
            ? "You've reached the maximum of 7 recipes for this week. Remove some to select others."
            : "Choose from our delicious recipe collection"}
        </p>
      </div>

      <div className="grid gap-6 grid-cols-[repeat(auto-fit,minmax(340px,1fr))] justify-center">
        {recipes.map((recipe, index) => {
          const selected = isRecipeSelected(recipe.id);
          const canSelect = selected || !maxSelectionsReached;

          return (
            <motion.div
              key={recipe.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{
                duration: 0.4,
                delay: index * 0.05,
                type: "spring",
                stiffness: 300,
                damping: 30,
              }}
              className={`${!canSelect ? "opacity-60 pointer-events-none" : ""}`}
            >
              <RecipeCard
                recipe={recipe}
                selected={selected}
                onSelect={() => {
                  if (canSelect) {
                    onRecipeSelect(recipe.id);
                  }
                }}
              />
            </motion.div>
          );
        })}
      </div>

      {maxSelectionsReached && (
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          className="mt-6 p-4 bg-warning/10 border border-warning/20 rounded-lg text-center"
        >
          <p className="text-sm text-warning-foreground">
            You've selected the maximum of 7 recipes for this week. To select a
            different recipe, please remove one from your current selection.
          </p>
        </motion.div>
      )}
    </motion.section>
  );
}
