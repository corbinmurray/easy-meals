"use client";

import RecipeCard from "@/components/RecipeCard";
import type { Recipe } from "@/types/meal-planner";
import { Badge } from "@workspace/ui/components/badge";
import { Checkbox } from "@workspace/ui/components/checkbox";
import { Input } from "@workspace/ui/components/input";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@workspace/ui/components/pagination";
import { Search } from "lucide-react";
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

  // Search and filter logic
  const filteredRecipes = useMemo(() => {
    let filtered = [...recipes];

    // Apply search filter
    if (enableSearch && debouncedSearchTerm.trim()) {
      const searchLower = debouncedSearchTerm.toLowerCase();
      filtered = filtered.filter((recipe) => {
        // Search in name
        if (recipe.name.toLowerCase().includes(searchLower)) return true;

        // Search in description
        if (recipe.description.toLowerCase().includes(searchLower)) return true;

        // Search in tags
        if (recipe.tags?.some((tag) => tag.toLowerCase().includes(searchLower)))
          return true;

        return false;
      });
    }

    // Apply tag filtering (AND logic - all selected tags must be present)
    if (enableTagFiltering && selectedTags.length > 0) {
      filtered = filtered.filter((recipe) => {
        return selectedTags.every((selectedTag) =>
          recipe.tags?.includes(selectedTag)
        );
      });
    }

    return filtered;
  }, [
    recipes,
    debouncedSearchTerm,
    selectedTags,
    enableSearch,
    enableTagFiltering,
  ]);

  // Pagination logic
  const totalPages = enablePagination
    ? Math.ceil(filteredRecipes.length / recipesPerPage)
    : 1;
  const paginatedRecipes = enablePagination
    ? filteredRecipes.slice(
        (currentPage - 1) * recipesPerPage,
        currentPage * recipesPerPage
      )
    : filteredRecipes;

  // Ensure current page is valid when filters change
  useEffect(() => {
    if (currentPage > totalPages && totalPages > 0) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);
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

      {/* Search and Filter Controls */}
      <div className="mb-6 space-y-4">
        {/* Search Input */}
        {enableSearch && (
          <motion.div
            className="relative"
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
          >
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
            <Input
              type="text"
              placeholder="Search recipes by name, description, or tags..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="pl-10 w-full"
            />
          </motion.div>
        )}

        {/* Tag Filtering */}
        {enableTagFiltering && availableTags.length > 0 && (
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3, delay: 0.1 }}
          >
            <h4 className="text-sm font-medium mb-3">Filter by Tags</h4>
            <div className="flex flex-wrap gap-2">
              {availableTags.map((tag) => {
                const isSelected = selectedTags.includes(tag);
                return (
                  <motion.div
                    key={tag}
                    whileHover={{ scale: 1.02 }}
                    whileTap={{ scale: 0.98 }}
                  >
                    <Badge
                      variant={isSelected ? "default" : "secondary"}
                      className={`cursor-pointer transition-colors ${
                        isSelected ? "bg-primary text-primary-foreground" : ""
                      }`}
                      onClick={() => {
                        setSelectedTags((prev) =>
                          isSelected
                            ? prev.filter((t) => t !== tag)
                            : [...prev, tag]
                        );
                      }}
                    >
                      <Checkbox
                        checked={isSelected}
                        className="mr-2 h-3 w-3"
                        readOnly
                      />
                      {tag}
                    </Badge>
                  </motion.div>
                );
              })}
            </div>
          </motion.div>
        )}
      </div>

      <div className="grid gap-6 grid-cols-[repeat(auto-fit,minmax(340px,1fr))] justify-center">
        {paginatedRecipes.map((recipe, index) => {
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

      {/* No results message */}
      {filteredRecipes.length === 0 && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          className="text-center py-12"
        >
          <p className="text-muted-foreground text-lg">
            No recipes found matching your search criteria.
          </p>
          {(debouncedSearchTerm || selectedTags.length > 0) && (
            <p className="text-sm text-muted-foreground mt-2">
              Try adjusting your search terms or clearing filters.
            </p>
          )}
        </motion.div>
      )}

      {/* Pagination */}
      {enablePagination && totalPages > 1 && filteredRecipes.length > 0 && (
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
          className="mt-8 flex justify-center"
        >
          <Pagination>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious
                  onClick={() =>
                    setCurrentPage((prev) => Math.max(1, prev - 1))
                  }
                  className={
                    currentPage === 1
                      ? "pointer-events-none opacity-50"
                      : "cursor-pointer"
                  }
                />
              </PaginationItem>

              {Array.from({ length: totalPages }, (_, i) => i + 1).map(
                (page) => (
                  <PaginationItem key={page}>
                    <PaginationLink
                      onClick={() => setCurrentPage(page)}
                      isActive={currentPage === page}
                      className="cursor-pointer"
                    >
                      {page}
                    </PaginationLink>
                  </PaginationItem>
                )
              )}

              <PaginationItem>
                <PaginationNext
                  onClick={() =>
                    setCurrentPage((prev) => Math.min(totalPages, prev + 1))
                  }
                  className={
                    currentPage === totalPages
                      ? "pointer-events-none opacity-50"
                      : "cursor-pointer"
                  }
                />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </motion.div>
      )}

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
