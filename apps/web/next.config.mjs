/** @type {import('next').NextConfig} */
const nextConfig = {
  transpilePackages: ["@workspace/ui"],
  output: "standalone",
  async rewrites() {
    return process.env.NODE_ENV !== "production" 
      ? [
          {
            source: "/api/:path*",
            destination: "http://localhost:8080/api/:path*",
          },
        ]
      : [];
  }
}

export default nextConfig
