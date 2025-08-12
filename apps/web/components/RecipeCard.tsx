import type { Recipe } from "@/types/meal-planner";
import { Badge } from "@workspace/ui/components/badge";
import { Button } from "@workspace/ui/components/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardTitle,
} from "@workspace/ui/components/card";
import { CheckCircle2 } from "lucide-react";

interface RecipeCardProps {
  recipe: Recipe;
  selected: boolean;
  onSelect: () => void;
}

export default function RecipeCard({
  recipe,
  selected,
  onSelect,
}: RecipeCardProps) {
  return (
    <Card
      className={`group relative flex flex-col h-full min-h-[380px] max-h-[420px] transition-all duration-300 ease-in-out hover:scale-[1.02] p-0 ${
        selected
          ? "ring-2 ring-primary shadow-lg shadow-primary/20"
          : "hover:shadow-xl hover:shadow-black/10"
      }`}
    >
      {/* Image section with overlay gradient */}
      <div className="relative h-32 w-full overflow-hidden rounded-t-lg bg-gradient-to-br from-muted to-muted/50">
        {recipe.image ? (
          <div
            className="absolute inset-0 bg-cover bg-center transition-transform duration-300 group-hover:scale-105"
            style={{ backgroundImage: `url(${recipe.image})` }}
          />
        ) : (
          <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-accent/10">
            <div className="text-6xl font-bold text-primary/30">
              {recipe.name.slice(0, 2).toUpperCase()}
            </div>
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/30 via-transparent to-transparent" />
      </div>

      {/* Content section */}
      <div className="flex flex-col flex-1 p-4">
        {/* Title and tags */}
        <div className="space-y-3">
          <CardTitle className="text-lg font-semibold leading-tight line-clamp-2 group-hover:text-primary transition-colors">
            {recipe.name}
          </CardTitle>

          {/* Tags */}
          {recipe.tags && recipe.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5 min-h-[24px]">
              {recipe.tags.slice(0, 3).map((tag) => (
                <Badge
                  key={tag}
                  variant="secondary"
                  className="text-xs px-2 py-0.5 bg-secondary/80 hover:bg-secondary transition-colors"
                >
                  {tag}
                </Badge>
              ))}
              {recipe.tags.length > 3 && (
                <Badge variant="outline" className="text-xs px-2 py-0.5">
                  +{recipe.tags.length - 3}
                </Badge>
              )}
            </div>
          )}
        </div>

        {/* Description */}
        <CardContent className="flex-1 px-0 pt-3 pb-0">
          <CardDescription className="text-sm text-muted-foreground line-clamp-3 leading-relaxed">
            {recipe.description}
          </CardDescription>
        </CardContent>

        {/* Action button */}
        <CardFooter className="px-0 pt-4 pb-0">
          <Button
            variant={selected ? "default" : "outline"}
            onClick={onSelect}
            className={`w-full transition-all duration-200 ${
              selected
                ? "bg-primary hover:bg-primary/90 shadow-md"
                : "hover:bg-primary hover:text-primary hover:border-primary"
            }`}
          >
            {selected ? <CheckCircle2 className="w-4 h-4" /> : "Select Recipe"}
          </Button>
        </CardFooter>
      </div>
    </Card>
  );
}
