const webpackMerge = require("webpack-merge");

const baseWebpackConfig = require("@kentico/xperience-webpack-config");

// orgName/projectName must match RegisterClientModule in LuceneSuiteAdminModule.cs and
// AdminOrgName/ProjectName in the csproj; devServer.port must match CMSAdminClientModuleSettings.
module.exports = (opts, argv) => {
  const baseConfig = (webpackConfigEnv, argv) =>
    baseWebpackConfig({
      orgName: "xperience-community",
      projectName: "lucene-suite",
      webpackConfigEnv: webpackConfigEnv,
      argv: argv,
    });

  const projectConfig = {
    module: {
      rules: [
        {
          test: /\.(js|ts)x?$/,
          exclude: [/node_modules/],
          loader: "babel-loader",
        },
      ],
    },
    output: {
      clean: true,
    },
    devServer: {
      port: 3011,
    },
  };

  return webpackMerge.merge(projectConfig, baseConfig(opts, argv));
};
