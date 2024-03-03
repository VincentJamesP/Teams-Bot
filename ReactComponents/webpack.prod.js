const path = require('path')
const MiniCssExtractPlugin = require('mini-css-extract-plugin')
const HtmlWebPackPlugin = require('html-webpack-plugin')
const TerserPlugin = require('terser-webpack-plugin')

module.exports = {
    mode: 'production',
    entry: {
        swap: './src/extensions/swap.js',
        reserve: './src/tabs/reserve.js'
    },
    output: {
        filename: 'js/[name].js',
        path: path.join(__dirname, 'public'),
        hashFunction: "xxhash64",
        chunkFilename: '[name]'
    },

    module: {
        rules: [
            {
                test: /\.js$/,
                loader: 'babel-loader',
                exclude: /node_modules/
            },
            {
                test: /\.css$/,
                use: [
                    MiniCssExtractPlugin.loader,
                    'css-loader', 'postcss-loader'
                ]
            },
            {
                test: /\.(eot|svg|ttf|otf|woff|woff2|png|jpg|gif)$/i,
                type: 'asset'
            },
        ]
    },

    plugins: [
        new HtmlWebPackPlugin({
            template: './src/extensions/swap.html',
            filename: './extensions/swap.html',
            chunks: ['swap']
        }),
        new HtmlWebPackPlugin({
            template: './src/tabs/reserve.html',
            filename: './tabs/reserve.html',
            chunks: ['reserve']
        })
    ],

    optimization: {
        minimize: true,
        minimizer: [new TerserPlugin],
        splitChunks: {
            chunks: 'all',
            minChunks: 2
        }
    }
}
