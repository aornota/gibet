// Based on https://github.com/fable-compiler/webpack-config-template.

var path = require("path");
var webpack = require("webpack");
var htmlWebpackPlugin = require('html-webpack-plugin');
var copyWebpackPlugin = require('copy-webpack-plugin');
var miniCssExtractPlugin = require("mini-css-extract-plugin");

var port = process.env.SERVER_PORT || "8085"

var config = {
    indexHtmlTemplate: "./src/ui/index.html"
    , fsharpEntry: "./src/ui/ui.fsproj"
    , sassEntry: "./src/ui/style/gibet-bulma.sass"
    , cssEntry: "./src/ui/style/gibet.css"
    , outputDir: "./src/ui/deploy"
    , assetsDir: "./src/ui/public"
    , devServerPort: 8080
    , devServerProxy: {
        '/api/*': {
            target: 'http://localhost:' + port
            , changeOrigin: true
        }
        , '/bridge': {
            target: 'http://localhost:' + port
            , ws: true
        }
    }
    , babel: {
        presets: [
            ["@babel/preset-env", {
                "targets": "> 0.25%, not dead"
                , "modules": false
                , "useBuiltIns": "usage"
				, corejs: "2.6.5"
            }]
        ]
    }
}

var isProduction = !process.argv.find(v => v.indexOf('webpack-dev-server') !== -1);
console.log("Bundling for " + (isProduction ? "production" : "development") + "...");

// HtmlWebpackPlugin automatically injects <script> or <link> tags for generated bundles.
var commonPlugins = [
    new htmlWebpackPlugin({
        filename: 'index.html'
        , template: resolve(config.indexHtmlTemplate)
    })
];

module.exports = {
    // In development, bundle styles together with the code so they can also trigger hot reloads; in production, put them in a separate CSS file.
    entry: isProduction ? {
        app: [resolve(config.fsharpEntry), resolve(config.sassEntry), resolve(config.cssEntry)]
    } : {
            app: [resolve(config.fsharpEntry)]
            , style: [resolve(config.sassEntry), resolve(config.cssEntry)]
        }
    , output: {
        path: resolve(config.outputDir)
        , filename: isProduction ? '[name].[hash].js' : '[name].js'
    }
    , mode: isProduction ? "production" : "development"
    , devtool: isProduction ? "source-map" : "eval-source-map"
    , optimization: {
        splitChunks: {
            cacheGroups: {
                commons: {
                    test: /node_modules/
                    , name: "vendors"
                    , chunks: "all"
                }
            }
        }
    }
    , plugins: isProduction
        ? commonPlugins.concat([
            new miniCssExtractPlugin({ filename: 'style.css' })
            , new copyWebpackPlugin([{ from: resolve(config.assetsDir) }])
        ])
        : commonPlugins.concat([
            new webpack.HotModuleReplacementPlugin()
        ])
    , resolve: {
        symlinks: false
    }
    , devServer: {
        publicPath: "/"
        , contentBase: resolve(config.assetsDir)
        , port: config.devServerPort
        , proxy: config.devServerProxy
        , hot: true
        , inline: true
    }
    , module: {
        rules: [
            {
                test: /\.fs(x|proj)?$/
                , use: {
                    loader: "fable-loader"
                    , options: {
                        babel: config.babel
                        , define: isProduction ? [ "ACTIVITY", "TICK" ] : [ "DEBUG", "ACTIVITY"/*, "HMR"*/, "TICK" ] // TODO-NMB: Review whether to use HMR (&c.)...
                    }
                }
            }
            , {
                test: /\.js$/
                , exclude: /node_modules/
                , use: {
                    loader: 'babel-loader'
                    , options: config.babel
                },
            }
            , {
                test: /\.(sass|scss|css)$/
                , use: [
                    isProduction ? miniCssExtractPlugin.loader : 'style-loader'
                    , 'css-loader'
                    , {
                        loader: 'sass-loader'
                        , options: { implementation: require("sass") }
                    }
                ]
            }
            , {
                test: /\.(png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)(\?.*)?$/
                , use: ["file-loader"]
            }
        ]
    }
};

function resolve(filePath) {
    return path.isAbsolute(filePath) ? filePath : path.join(__dirname, filePath);
}
