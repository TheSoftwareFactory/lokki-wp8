"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
#! coding: utf-8
import os
import shutil
import time

from os.path import abspath, dirname, basename, join, exists
from scripts import buildutils, code_analysis_to_junit
from scripts import create_ta_package
from optparse import OptionParser

import run_unittests
try:
    import build_config as CONFIG
except:
    import default_build_config as CONFIG


THISDIR = dirname(abspath(__file__))

# Created by msbuild
ANALYZED_LOG = join("src", "msbuild1.log")
ANALYSIS_JUNIT = "msbuild-analysis-junit.xml"

def build_platform(platform, target, options, version):
        
    #if platform == "x86_arm":
    #    platform_name = "ARM"
    #else:
    #    platform_name = "x86"
    
    platform_name = platform
    platform = "x86_arm"
    

    used_version = version
    if(target != "TestAutomation"):
        used_version = "_" + version

    target_filename = "%s_%s_%s%s.xap" %( options.appname,
        options.custom,
        #options.customsub,
        target,
        #platform_name,
        used_version)

    print "Building", target_filename
    print "=" * 40

    cmd = r'"C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\WPSDK\WP80\vcvarsphoneall.bat" %s' % ( platform )

    buildutils.call(cmd, cwd = THISDIR).apply_environment()

    cmd = 'msbuild /t:rebuild /property:Platform="%s" /property:Configuration="%s"' % ( platform_name, target )
    if "Release" in target:
        cmd += " /fileLogger1" # to create msbuild1.log

    buildutils.call(cmd, cwd = THISDIR)

    filename = "Lokki_%s.xap" % ( target )
    source = join(THISDIR, "Bin", target, filename)
    target = join(THISDIR, "build_output", target_filename)
    output_dir = join(THISDIR, "build_output/")

    if not exists(output_dir):

      for x in xrange(3):
          try:
              os.mkdir(output_dir)
              break
          except WindowsError:
              pass
      else:
          raise Exception("Failed to create " + output_dir)

    shutil.copy(source, target)

def main():
    parser = OptionParser( )
    #parser.add_option("-p", "--platforms",
    #                  default = "all",
    #                  help = "Comma delimited list of platforms to build: x86, x86_arm, all. default:%default")
    parser.add_option("-t", "--targets",
                      default = CONFIG.Targets,
                      help = "Comma delimited list of targets: Debug, Release, TestAutomation, all. default:%default")
    parser.add_option("", "--appname",
                      default = CONFIG.AppName,
                      help = "default:%default")
    parser.add_option("", "--custom",
                      default = CONFIG.Custom,
                      help = "default:%default")
    parser.add_option("", "--customsub",
                      default = CONFIG.CustomSub,
                      help = "default:%default")
    parser.add_option("", "--no-unittests",
                      default = True,
                      action="store_true",
                      help = "default:%default")

    run_unittests.create_options(parser)

    (options, args) = parser.parse_args()
    options.platforms = "Any CPU"
    #if options.platforms == "all":
    #    options.platforms = "x86,x86_arm"

    if options.targets == "all":
        options.targets = "Debug,Release,BetaRelease"

    options.targets = options.targets.split(",")
    options.platforms = options.platforms.split(",")

    # Clear output dir
    output_dir = join(THISDIR, "build_output/")

    if exists(output_dir):
        shutil.rmtree(output_dir, ignore_errors = True)

    ver = "%s %s" % (CONFIG.Custom, CONFIG.Version)
    print "=== Building version %s ===" % (ver)

    for p in options.platforms:
        for t in options.targets:
            build_platform(p, t, options, CONFIG.Version)

    #print "=== Creating TA package ==="
    #create_ta_package.create(ver, join( output_dir, "test-automation-runner.py"))

    # Parse code analysis and create junit result file for Jenkins
    if not options.no_unittests:
        print "=== Running UnitTests ==="
        run_unittests.process(options)

        with open(ANALYZED_LOG) as f:
            data = f.read()
            with open(ANALYSIS_JUNIT, 'w') as out:
                xml = code_analysis_to_junit.to_xml_string( code_analysis_to_junit.generate(data) )
                print xml
                out.write(xml)

if __name__ == "__main__":
    main()