# Source files and their filters
include(CMakeSources.cmake)
	
# Target
if(WIN32)
	add_executable(Game WIN32 ${BS_GAME_SRC})
else()
	add_executable(Game ${BS_GAME_SRC})
endif()

# Common flags
add_common_flags(Game)

# Includes
target_include_directories(Game PRIVATE "./")

# Post-build step
if(WIN32)
	if(BS_64BIT)
		set(BS_COPY_FOLDER Win64)
	else()
		set(BS_COPY_FOLDER Win32)
	endif()

	if(MSVC)
		add_custom_command(
			TARGET Game POST_BUILD
			COMMAND xcopy /Y /I \"$(TargetDir)$(TargetName).exe\" \"${PROJECT_SOURCE_DIR}\\Data\\Binaries\\${BS_COPY_FOLDER}\\\")
	else()
		set(SRC_FILE ${BS_BINARY_OUTPUT_DIR}/${CMAKE_BUILD_TYPE}/Game)
		set(DST_FOLDER ${PROJECT_SOURCE_DIR}/Data/Binaries/${BS_COPY_FOLDER})
		add_custom_command(TARGET Game POST_BUILD COMMAND xcopy /Y /I ${SRC_FILE} ${DST_FOLDER})
	endif()

elseif(LINUX)
	if(BS_64BIT)
		set(BS_COPY_FOLDER Linux64)
	else()
		set(BS_COPY_FOLDER Linux32)
	endif()

	set(SRC_FILE ${BS_BINARY_OUTPUT_DIR}/${CMAKE_BUILD_TYPE}/Game)
	set(DST_FOLDER ${PROJECT_SOURCE_DIR}/Data/Binaries/${BS_COPY_FOLDER})
	add_custom_command(
		TARGET Game POST_BUILD
		COMMAND rm -rf ${DST_FOLDER}
		COMMAND mkdir -p ${DST_FOLDER}
		COMMAND cp ${SRC_FILE} ${DST_FOLDER})
else()
# TODO_OTHER_PLATFORMS_GO_HERE
endif()

# Libraries
## Local libs
target_link_libraries(Game bsfScript bsfMono bsf)

# IDE specific
set_property(TARGET Game PROPERTY FOLDER Executable)

# Plugin dependencies
add_engine_dependencies(Game)

# Compiler flags
if(MSVC)
	target_compile_options(Game PUBLIC /wd4509)
endif()

# Install
set(PLATFORM_PATH_PREFIX "")
if(WIN32)
	set(PLATFORM_PATH_PREFIX "Win")
elseif(LINUX)
	set(PLATFORM_PATH_PREFIX "Linux")
elseif(APPLE)
	set(PLATFORM_PATH_PREFIX "macOS")
else()
	message(FATAL "Unsupported platform.")
endif()

if(BS_64BIT)
	set(PLATFORM_PATH_PREFIX "${PLATFORM_PATH_PREFIX}64")
endif()
	
install(
	TARGETS Game
	RUNTIME DESTINATION bin/EditorData/Binaries/${PLATFORM_PATH_PREFIX}
)
